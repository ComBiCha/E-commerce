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
using E_commerce.Services;
using PayPal.Api;
using Stripe;

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
                        user.SpinCount += 1;

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
            // Find the order
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return NotFound();
            }

            // Get order details with variations and products
            var orderDetails = await _dataContext.OrderDetails
                .Include(od => od.Variation)
                .ThenInclude(v => v.Product)
                .Where(od => od.OrderCode == ordercode)
                .ToListAsync();

            // Begin transaction to ensure data consistency
            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                // Restock products and update sold quantities
                foreach (var orderDetail in orderDetails)
                {
                    if (orderDetail.Variation != null)
                    {
                        // Restock by adding quantity back to ProductQuantityModel (preferably to the latest batch)
                        var productQuantities = await _dataContext.ProductQuantities
                            .Where(pq => pq.VariationId == orderDetail.Variation.Id)
                            .OrderByDescending(pq => pq.DateCreated) // Prefer latest batch for restocking
                            .ToListAsync();

                        int quantityToRestock = orderDetail.Quantity;

                        if (productQuantities.Any())
                        {
                            // Add quantity to the latest batch
                            var latestBatch = productQuantities.First();
                            latestBatch.CurrentQuantityInBatch += quantityToRestock;
                            latestBatch.LastUpdated = DateTime.Now;
                            _dataContext.ProductQuantities.Update(latestBatch);
                        }
                        else
                        {
                            // If no batch exists, create a new one (optional, depending on business rules)
                            var newBatch = new BatchModel
                            {
                                BatchCode = $"RESTOCK-{ordercode}-{DateTime.Now.Ticks}",
                                ImportDate = DateTime.Now,
                            };
                            _dataContext.Batches.Add(newBatch);
                            await _dataContext.SaveChangesAsync(); // Save to generate BatchId

                            var newProductQuantity = new ProductQuantityModel
                            {
                                VariationId = orderDetail.Variation.Id,
                                BatchId = newBatch.Id,
                                InitialQuantity = quantityToRestock,
                                CurrentQuantityInBatch = quantityToRestock,
                                DateCreated = DateTime.Now,
                                LastUpdated = DateTime.Now
                            };
                            _dataContext.ProductQuantities.Add(newProductQuantity);
                        }

                        // Update product sold quantity
                        if (orderDetail.Variation.Product != null)
                        {
                            orderDetail.Variation.Product.Sold -= orderDetail.Quantity;
                            if (orderDetail.Variation.Product.Sold < 0)
                            {
                                orderDetail.Variation.Product.Sold = 0;
                            }
                            _dataContext.Products.Update(orderDetail.Variation.Product);
                        }
                    }
                }

                // Process refund if applicable
                if (!string.IsNullOrEmpty(order.PaymentIntentId))
                {
                    if (order.PaymentIntentId.StartsWith("pi_"))
                    {
                        await ProcessStripeRefund(order.PaymentIntentId);
                    }
                    else if (order.PaymentIntentId.StartsWith("PAYID-"))
                    {
                        await ProcessPayPalRefund(order.PaymentIntentId);
                    }
                }

                // Remove order details and order
                if (orderDetails.Any())
                {
                    _dataContext.OrderDetails.RemoveRange(orderDetails);
                }
                _dataContext.Orders.Remove(order);

                // Save changes and commit transaction
                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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

            // Bắt đầu giao dịch để đảm bảo tính toàn vẹn dữ liệu
            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật thông tin đơn hàng
                order.ShippingCost = model.Order.ShippingCost;
                order.Address = model.Order.Address;
                order.UserName = model.Order.UserName;
                order.Status = 1;
                _dataContext.Orders.Update(order);

                foreach (var detail in model.OrderDetails)
                {
                    var existingDetail = await _dataContext.OrderDetails
                        .Include(x => x.Product)
                        .Include(x => x.Variation)
                        .FirstOrDefaultAsync(x => x.Id == detail.Id);

                    if (existingDetail == null)
                    {
                        ModelState.AddModelError(string.Empty, "Chi tiết đơn hàng không tồn tại.");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    // Tính sự chênh lệch số lượng
                    var quantityDifference = detail.Quantity - existingDetail.Quantity;

                    // Kiểm tra nếu Quantity lớn hơn Stock
                    if (existingDetail.Variation == null)
                    {
                        ModelState.AddModelError(string.Empty, "Không tìm thấy thông tin biến thể sản phẩm.");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    if (detail.Quantity <= 0)
                    {
                        ModelState.AddModelError(string.Empty, $"Số lượng không hợp lệ cho sản phẩm {existingDetail.Product.Name}");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    if (quantityDifference > 0 && quantityDifference > existingDetail.Variation.Stock)
                    {
                        ModelState.AddModelError(string.Empty, $"Số lượng của sản phẩm '{existingDetail.Product.Name}' vượt quá tồn kho. Hiện tại chỉ còn {existingDetail.Variation.Stock}.");
                        await transaction.RollbackAsync();
                        return View(model);
                    }

                    // Cập nhật Quantity trong OrderDetails
                    existingDetail.Quantity = detail.Quantity;

                    // Cập nhật tồn kho trong ProductQuantityModel
                    if (quantityDifference != 0)
                    {
                        var productQuantities = await _dataContext.ProductQuantities
                            .Where(pq => pq.VariationId == existingDetail.Variation.Id && pq.CurrentQuantityInBatch > 0)
                            .OrderBy(pq => pq.DateCreated) // FIFO
                            .ToListAsync();

                        if (quantityDifference > 0)
                        {
                            // Trừ số lượng từ các lô (FIFO)
                            int quantityToDeduct = quantityDifference;
                            foreach (var pq in productQuantities)
                            {
                                if (quantityToDeduct <= 0) break;

                                int deduct = Math.Min(pq.CurrentQuantityInBatch, quantityToDeduct);
                                pq.CurrentQuantityInBatch -= deduct;
                                pq.LastUpdated = DateTime.Now;
                                quantityToDeduct -= deduct;
                                _dataContext.ProductQuantities.Update(pq);
                            }
                        }
                        else if (quantityDifference < 0)
                        {
                            // Hoàn lại số lượng vào lô mới nhất
                            int quantityToRestock = -quantityDifference;
                            var latestBatch = await _dataContext.ProductQuantities
                                .Where(pq => pq.VariationId == existingDetail.Variation.Id)
                                .OrderByDescending(pq => pq.DateCreated)
                                .FirstOrDefaultAsync();

                            if (latestBatch != null)
                            {
                                latestBatch.CurrentQuantityInBatch += quantityToRestock;
                                latestBatch.LastUpdated = DateTime.Now;
                                _dataContext.ProductQuantities.Update(latestBatch);
                            }
                            else
                            {
                                // Tạo lô mới nếu không có lô nào
                                var newBatch = new BatchModel
                                {
                                    BatchCode = $"RESTOCK-EDIT-{order.OrderCode}-{DateTime.Now.Ticks}",
                                    ImportDate = DateTime.Now,
                                };
                                _dataContext.Batches.Add(newBatch);
                                await _dataContext.SaveChangesAsync(); // Lưu để lấy BatchId

                                var newProductQuantity = new ProductQuantityModel
                                {
                                    VariationId = existingDetail.Variation.Id,
                                    BatchId = newBatch.Id,
                                    InitialQuantity = quantityToRestock,
                                    CurrentQuantityInBatch = quantityToRestock,
                                    DateCreated = DateTime.Now,
                                    LastUpdated = DateTime.Now
                                };
                                _dataContext.ProductQuantities.Add(newProductQuantity);
                            }
                        }
                    }

                    // Cập nhật số lượng đã bán trong Product
                    if (existingDetail.Product != null)
                    {
                        existingDetail.Product.Sold += quantityDifference;
                        if (existingDetail.Product.Sold < 0)
                        {
                            existingDetail.Product.Sold = 0;
                        }
                        _dataContext.Products.Update(existingDetail.Product);
                    }

                    // Cập nhật chi tiết đơn hàng
                    _dataContext.OrderDetails.Update(existingDetail);
                }

                // Lưu tất cả thay đổi và commit giao dịch
                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction("Index"); // Chuyển hướng về danh sách đơn hàng
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi cập nhật đơn hàng: " + ex.Message);
                return View(model);
            }
        }

        private async Task ProcessStripeRefund(string paymentIntentId)
		{
			try
			{
				var refundOptions = new RefundCreateOptions
				{
					PaymentIntent = paymentIntentId,
					Reason = "requested_by_customer"
				};
				var refundService = new RefundService();
				var refund = await refundService.CreateAsync(refundOptions);

				if (refund.Status == "succeeded")
				{
					TempData["success"] = "Stripe refund processed successfully.";
				}
				else
				{
					TempData["error"] = "Stripe refund processing failed.";
				}
			}
			catch (Exception ex)
			{
				TempData["error"] = "Error processing Stripe refund: " + ex.Message;
			}
		}

		private async Task ProcessPayPalRefund(string paymentId)
		{
			try
			{
				var apiContext = new PayPalSdk().GetAPIContext(); // Lấy APIContext từ PayPalSdk

				// Lấy thông tin Payment từ PayPal
				var payment = Payment.Get(apiContext, paymentId);

				// Kiểm tra nếu không có giao dịch nào
				if (payment.transactions.Count == 0 || payment.transactions[0].related_resources.Count == 0)
				{
					TempData["error"] = "No transactions found for this PayPal payment.";
					return;
				}

				// Lấy Sale ID từ giao dịch đầu tiên
				var saleId = payment.transactions[0].related_resources[0].sale.id;

				// Tạo yêu cầu hoàn tiền
				var refundRequest = new RefundRequest
				{
					amount = new Amount
					{
						total = payment.transactions[0].amount.total, // Tổng tiền hoàn
						currency = payment.transactions[0].amount.currency // Đơn vị tiền tệ
					}
				};

				// Gọi Refund API
				var refund = Sale.Refund(apiContext, saleId, refundRequest); // ✅ Gọi đúng cách

				if (refund.state.ToLower() == "completed")
				{
					TempData["success"] = "PayPal refund processed successfully.";
				}
				else
				{
					TempData["error"] = "PayPal refund processing failed.";
				}
			}
			catch (Exception ex)
			{
				TempData["error"] = "Error processing PayPal refund: " + ex.Message;
			}
		}


	}
}
