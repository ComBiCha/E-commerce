using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_commerce.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using E_commerce.Repository;
using Stripe.Climate;
using E_commerce.Areas.Admin.Repository;
using E_commerce.Models.ViewModel;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly OrderSubject _orderSubject;

        public OrderController(DataContext context, OrderSubject orderSubject)
        {
            _dataContext = context;
            _orderSubject = orderSubject;
        }

        // Hiển thị danh sách đơn hàng
        public async Task<IActionResult> Index()
        {
            var orders = await _dataContext.Orders
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();
            return View(orders);
        }

        // Xem chi tiết đơn hàng
        public async Task<IActionResult> ViewOrder(string ordercode)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return NotFound();
            }

            // Lấy thông tin người dùng để tính giảm giá (nếu có)
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == order.UserName);
            decimal discountRate = user?.GetDiscountRate() ?? 0m;

            // Tính tổng tiền hàng dựa trên OrderDetails
            decimal productTotal = await _dataContext.OrderDetails
                .Where(o => o.OrderCode == ordercode)
                .SumAsync(o => (o.Price * o.Quantity) - o.DiscountAmount);

            decimal discountAmount = productTotal * discountRate;

            // Truy vấn chi tiết đơn hàng
            var detailsOrder = await _dataContext.OrderDetails
                .Include(o => o.Product)
                .ThenInclude(p => p.Warranty)
                .Include(o => o.Variation)
                .Include(o => o.Variation.Material)
                .Include(o => o.Variation.Color)
                .Where(o => o.OrderCode == ordercode)
                .ToListAsync();

            // Truyền dữ liệu qua View
            ViewBag.Order = order;
            ViewBag.DiscountRate = discountRate;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.ProductTotal = productTotal;

            return View(detailsOrder);
        }

        // Cập nhật trạng thái đơn hàng
        [HttpPost]
        public async Task<IActionResult> UpdateOrder(string ordercode, int status)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            order.Status = status;

            try
            {
                await _dataContext.SaveChangesAsync();

                // Nếu đơn hàng hoàn thành (Status = 5), cộng điểm cho user
                if (status == 5)
                {
                    var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == order.UserName);
                    if (user != null)
                    {
                        var totalAmount = await _dataContext.OrderDetails
                            .Where(od => od.OrderCode == ordercode)
                            .SumAsync(od => (od.Price * od.Quantity) - od.DiscountAmount);

                        user.Points += Convert.ToInt32(totalAmount);
                        await _dataContext.SaveChangesAsync();
                    }
                }

                await _orderSubject.NotifyObservers(order);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while updating the order status.",
                    error = ex.Message
                });
            }
        }

        // Xóa đơn hàng
        [HttpGet]
        public async Task<IActionResult> Delete(string ordercode)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return NotFound();
            }

            try
            {
                // Xóa chi tiết đơn hàng trước
                var orderDetails = await _dataContext.OrderDetails
                    .Where(od => od.OrderCode == ordercode)
                    .ToListAsync();

                if (orderDetails.Any())
                {
                    _dataContext.OrderDetails.RemoveRange(orderDetails);
                }

                // Xóa đơn hàng
                _dataContext.Orders.Remove(order);
                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while deleting the order: " + ex.Message);
                return RedirectToAction("Index");
            }

            return RedirectToAction("Index");
        }

        // Tạo bản sao đơn hàng bằng Prototype Pattern
        [HttpPost]
        public async Task<IActionResult> CloneOrder(string ordercode)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return Json(new { success = false, message = "Order not found" });
            }

            // Tạo bản sao đơn hàng
            string newOrderCode = $"CLONE-{order.OrderCode}-{DateTime.Now.Ticks}";
            var clonedOrder = new OrderModel
            {
                OrderCode = newOrderCode,
                ShippingCost = order.ShippingCost,
                Address = order.Address,
                UserName = order.UserName,
                CreatedDate = DateTime.Now,
                Status = order.Status,
                PaymentIntentId = order.PaymentIntentId
            };

            _dataContext.Orders.Add(clonedOrder);
            await _dataContext.SaveChangesAsync(); // Lưu để có Id của đơn hàng mới

            // Lấy chi tiết đơn hàng gốc
            var orderDetails = await _dataContext.OrderDetails
                .Where(od => od.OrderCode == ordercode)
                .ToListAsync();

            // Clone các chi tiết đơn hàng
            foreach (var detail in orderDetails)
            {
                var clonedDetail = new OrderDetails
                {
                    OrderCode = newOrderCode,
                    UserName = detail.UserName,
                    ProductId = detail.ProductId,
                    Price = detail.Price,
                    Quantity = 0,
                    DiscountAmount = detail.DiscountAmount,
                    VariationId = detail.VariationId
                };

                _dataContext.OrderDetails.Add(clonedDetail);
            }

            await _dataContext.SaveChangesAsync();

            return Json(new { success = true, newOrderId = clonedOrder.Id });
        }



        [HttpGet]
        public async Task<IActionResult> EditOrder(int id)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            var orderDetails = await _dataContext.OrderDetails
                .Where(od => od.OrderCode == order.OrderCode)
                .Include(od => od.Product) // Bao gồm Product
                .Include(od => od.Variation) // Bao gồm Variation
                .ThenInclude(v => v.Material) // Bao gồm Material của Variation
                .Include(od => od.Variation)
                .ThenInclude(v => v.Color) // Bao gồm Color của Variation
                .ToListAsync();

            // Tạo ViewModel
            var viewModel = new EditOrderViewModel
            {
                Order = order,
                OrderDetails = orderDetails ?? new List<OrderDetails>() // Đảm bảo không null
            };

            return View(viewModel);
        }




        [HttpPost]
        public async Task<IActionResult> EditOrder(EditOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model); // Trả về View với model để hiển thị lỗi
            }


            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.Id == model.Order.Id);
            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            // Cập nhật thông tin đơn hàng
            order.ShippingCost = model.Order.ShippingCost;
            order.Address = model.Order.Address;
            order.UserName = model.Order.UserName;
            order.Status = 1;

            _dataContext.Orders.Update(order);

            foreach (var detail in model.OrderDetails)
            {
                var existingDetail = _dataContext.OrderDetails
                    .Include(x => x.Product)
                    .Include(x => x.Variation)
                    .FirstOrDefault(x => x.Id == detail.Id);

                if (existingDetail != null)
                {
                    // Tính sự chênh lệch
                    var quantityDifference = detail.Quantity - existingDetail.Quantity;

                    // Kiểm tra nếu Quantity lớn hơn Stock
                    if (existingDetail.Variation == null)
                    {
                        ModelState.AddModelError(string.Empty, "Không tìm thấy thông tin biến thể sản phẩm.");
                        return View(model);
                    }

                    if (detail.Quantity <= 0)
                    {
                        ModelState.AddModelError(string.Empty, $"Số lượng không hợp lệ cho sản phẩm {existingDetail.Product.Name}");
                        return View(model);
                    }

                    if (existingDetail.Variation != null && detail.Quantity > existingDetail.Variation.Stock)
                    {
                        ModelState.AddModelError(string.Empty, $"Số lượng của sản phẩm '{existingDetail.Product.Name}' vượt quá tồn kho. Hiện tại chỉ còn {existingDetail.Variation.Stock}.");
                        return View(model);
                    }

                    // Cập nhật Quantity trong OrderDetails
                    existingDetail.Quantity = detail.Quantity;

                    // Trừ Stock và cộng Sold nếu có Product
                    if (existingDetail.Product != null)
                    {
                        existingDetail.Product.Quantity -= quantityDifference;
                        existingDetail.Product.Sold += quantityDifference;
                        _dataContext.Products.Update(existingDetail.Product);
                    }

                    // Trừ Stock nếu có Variation
                    if (existingDetail.Variation != null)
                    {
                        existingDetail.Variation.Stock -= quantityDifference;
                        _dataContext.Variations.Update(existingDetail.Variation);
                    }

                    // Cập nhật lại OrderDetails
                    _dataContext.OrderDetails.Update(existingDetail);
                }

                await _dataContext.SaveChangesAsync();
            }
            return RedirectToAction("Index"); // Chuyển hướng về danh sách đơn hàng
        }


    }
}
