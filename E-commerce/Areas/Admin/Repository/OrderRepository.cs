using Microsoft.EntityFrameworkCore;
using E_commerce.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using E_commerce;
using E_commerce.Areas.Admin.Repository;
using E_commerce.Models.ViewModel;
namespace E_commerce.Repository
{
    public class OrderRepository : IOrderRepository
    {
        private readonly DataContext _dataContext;
        private readonly OrderSubject _orderSubject;

        public OrderRepository(DataContext dataContext, OrderSubject orderSubject)
        {
            _dataContext = dataContext;
            _orderSubject = orderSubject;// Pattern Observer để thông báo thay đổi đơn hàng
        }

        //Lấy danh sách đơn hàng có phân trang
        public async Task<List<OrderModel>> GetAllOrdersAsync(int page = 1, int pageSize = 10, string sortBy = "CreatedDate", bool ascending = false)
        {
            var query = _dataContext.Orders.AsQueryable();

            if (ascending)
            {
                query = query.OrderBy(o => EF.Property<object>(o, sortBy));
            }
            else
            {
                query = query.OrderByDescending(o => EF.Property<object>(o, sortBy));
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        //Lấy thông tin đơn hàng theo mã đơn hàng
        public async Task<OrderModel> GetOrderByCodeAsync(string orderCode)
        {
            return await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        }
        //Cập nhật đơn hàng (Observer Pattern)
        public async Task UpdateOrderAsync(OrderModel order)
        {
            var existingOrder = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == order.OrderCode);
            if (existingOrder == null)
            {
                throw new KeyNotFoundException("Order not found");
            }

            // Cập nhật trạng thái đơn hàng
            existingOrder.Status = order.Status;
            _dataContext.Orders.Update(existingOrder);
            await _dataContext.SaveChangesAsync();

            // Nếu đơn hàng hoàn thành (Status = 5), cộng điểm cho user
            if (order.Status == 5)
            {
                var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == existingOrder.UserName);
                if (user != null)
                {
                    var totalAmount = await _dataContext.OrderDetails
                        .Where(od => od.OrderCode == existingOrder.OrderCode)
                        .SumAsync(od => (od.Price * od.Quantity) - od.DiscountAmount);

                    user.Points += Convert.ToInt32(totalAmount);
                    await _dataContext.SaveChangesAsync();
                }
            }

            // Gửi thông báo cho các observer
            if (_orderSubject != null)
            {
                await _orderSubject.NotifyObservers(existingOrder);
            }
        }


        //Xóa đơn hàng
        public async Task DeleteOrderAsync(string orderCode)
        {
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order != null)
            {
                _dataContext.Orders.Remove(order);
                await _dataContext.SaveChangesAsync();
            }
        }
        public async Task<OrderModel> GetOrderByIdAsync(int id)
        {
            return await _dataContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<OrderDetails>> GetOrderDetailsByOrderCodeAsync(string orderCode)
        {
            return await _dataContext.OrderDetails
                .Where(od => od.OrderCode == orderCode)
                .Include(od => od.Product)
                .Include(od => od.Variation)
                .ThenInclude(v => v.Material)
                .Include(od => od.Variation)
                .ThenInclude(v => v.Color)
                .ToListAsync();
        }
        // Cập nhật đơn hàng với chi tiết
        public async Task UpdateOrderWithDetailsAsync(EditOrderViewModel model)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.Id == model.Order.Id);
            if (order == null)
                throw new Exception("Order not found");

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật đơn hàng
                order.ShippingCost = model.Order.ShippingCost;
                order.Address = model.Order.Address;
                order.UserName = model.Order.UserName;
                order.Status = 1;

                _dataContext.Orders.Update(order);

                foreach (var detail in model.OrderDetails)
                {
                    var existingDetail = await _dataContext.OrderDetails
                        .Include(x => x.Product)
                            .ThenInclude(p => p.Variations)
                                .ThenInclude(v => v.ProductQuantities)
                        .Include(x => x.Variation)
                        .FirstOrDefaultAsync(x => x.Id == detail.Id);

                    if (existingDetail == null)
                        throw new Exception("Order detail not found");

                    if (existingDetail.Variation == null)
                        throw new Exception("Không tìm thấy thông tin biến thể sản phẩm.");

                    if (detail.Quantity <= 0)
                        throw new Exception($"Số lượng không hợp lệ cho sản phẩm {existingDetail.Product?.Name}");

                    if (detail.Quantity > existingDetail.Variation.Stock)
                        throw new Exception($"Sản phẩm '{existingDetail.Product?.Name}' chỉ còn {existingDetail.Variation.Stock} trong kho.");

                    int quantityDifference = detail.Quantity - existingDetail.Quantity;

                    // Cập nhật số lượng chi tiết
                    existingDetail.Quantity = detail.Quantity;
                    _dataContext.OrderDetails.Update(existingDetail);

                    if (quantityDifference != 0 && existingDetail.Product != null)
                    {
                        var productQuantities = await _dataContext.ProductQuantities
                            .Where(pq => pq.VariationId == existingDetail.Variation.Id && pq.CurrentQuantityInBatch > 0)
                            .OrderBy(pq => pq.DateCreated)
                            .ToListAsync();

                        if (quantityDifference > 0)
                        {
                            int quantityToDeduct = quantityDifference;
                            foreach (var pq in productQuantities)
                            {
                                if (quantityToDeduct <= 0) break;
                                int deduct = Math.Min(pq.CurrentQuantityInBatch, quantityToDeduct);
                                pq.CurrentQuantityInBatch -= deduct;
                                pq.LastUpdated = DateTime.UtcNow;
                                quantityToDeduct -= deduct;
                                _dataContext.ProductQuantities.Update(pq);
                            }
                        }
                        else if (quantityDifference < 0)
                        {
                            int quantityToRestock = -quantityDifference;
                            var latestBatch = productQuantities.OrderByDescending(pq => pq.DateCreated).FirstOrDefault();
                            if (latestBatch != null)
                            {
                                latestBatch.CurrentQuantityInBatch += quantityToRestock;
                                latestBatch.LastUpdated = DateTime.UtcNow;
                                _dataContext.ProductQuantities.Update(latestBatch);
                            }
                            else
                            {
                                var newBatch = new BatchModel
                                {
                                    BatchCode = $"RESTOCK-{order.Id}-{DateTime.UtcNow.Ticks}",
                                    ImportDate = DateTime.UtcNow,
                                };
                                _dataContext.Batches.Add(newBatch);
                                await _dataContext.SaveChangesAsync();

                                var newProductQuantity = new ProductQuantityModel
                                {
                                    VariationId = existingDetail.Variation.Id,
                                    BatchId = newBatch.Id,
                                    InitialQuantity = quantityToRestock,
                                    CurrentQuantityInBatch = quantityToRestock,
                                    DateCreated = DateTime.UtcNow,
                                    LastUpdated = DateTime.UtcNow
                                };
                                _dataContext.ProductQuantities.Add(newProductQuantity);
                            }
                        }

                        // Cập nhật số lượng đã bán (Sold)
                        existingDetail.Product.Sold += quantityDifference;
                        if (existingDetail.Product.Sold < 0)
                            existingDetail.Product.Sold = 0;

                        _dataContext.Products.Update(existingDetail.Product); // vẫn cần nếu bạn muốn lưu Sold
                    }
                }

                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Đã xảy ra lỗi khi cập nhật đơn hàng: {ex.Message}");
            }
        }

        // Clone đơn hàng
        public async Task<int> CloneOrderAsync(string orderCode)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
            if (order == null)
            {
                throw new Exception("Order not found");
            }

            // Dùng Builder để tạo bản sao đơn hàng
            string newOrderCode = $"CLONE-{order.OrderCode}-{DateTime.Now.Ticks}";
            var clonedOrder = new OrderBuilder()
                .SetOrderCode(newOrderCode)
                .SetShippingCost(order.ShippingCost)
                .SetAddress(order.Address)
                .SetUserName(order.UserName)
                .SetCreatedDate(DateTime.Now)
                .SetStatus(order.Status)
                .SetPaymentIntentId(order.PaymentIntentId)
                .Build();
            // Lấy chi tiết đơn hàng gốc
            var orderDetails = await _dataContext.OrderDetails
                .Where(od => od.OrderCode == orderCode)
                .ToListAsync();

            // Clone chi tiết đơn hàng
            foreach (var detail in orderDetails)
            {
                var clonedDetail = new OrderDetails
                {
                    OrderCode = newOrderCode,
                    UserName = detail.UserName,
                    ProductId = detail.ProductId,
                    Price = detail.Price,
                    Quantity = 0, // Set quantity = 0 để người dùng tự cập nhật
                    DiscountAmount = detail.DiscountAmount,
                    VariationId = detail.VariationId
                };

                _dataContext.OrderDetails.Add(clonedDetail);
            }

            await _dataContext.SaveChangesAsync();
            return clonedOrder.Id;
        }
        // Xem chi tiết đơn hàng
        public async Task<List<OrderDetails>> GetOrderDetailsAsync(string orderCode)
        {
            return await _dataContext.OrderDetails
                .Include(o => o.Product)
                    .ThenInclude(p => p.Warranty)
                .Include(o => o.Variation)
                    .ThenInclude(v => v.Material)
                .Include(o => o.Variation)
                    .ThenInclude(v => v.Color)
                .Where(od => od.OrderCode == orderCode)
                .ToListAsync();
        }

        // Lấy thông tin khách hàng và mã giảm giá
        public async Task<decimal> GetUserDiscountRateAsync(string userEmail)
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            return user?.GetDiscountRate() ?? 0m;
        }


        public async Task<decimal> CalculateProductTotalAsync(string orderCode)
        {
            return await _dataContext.OrderDetails
                .Where(od => od.OrderCode == orderCode)
                .SumAsync(od => (od.Price * od.Quantity) - od.DiscountAmount);
        }

        public async Task AddOrderAsync(OrderModel order)
        {
            _dataContext.Orders.Add(order);
            await _dataContext.SaveChangesAsync();
        }

        public async Task AddOrderDetailsAsync(List<OrderDetails> orderDetails)
        {
            _dataContext.OrderDetails.AddRange(orderDetails);
            await _dataContext.SaveChangesAsync();
        }
    }
}