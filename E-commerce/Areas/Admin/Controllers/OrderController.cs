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
                return NotFound(new { success = false, message = "Order not found" });
            }

            // Lấy danh sách OrderDetails của đơn hàng gốc
            var orderDetails = await _dataContext.OrderDetails.Where(od => od.OrderCode == ordercode).ToListAsync();

            // Tạo prototype từ đơn hàng gốc
            var orderPrototype = new OrderPrototype(order.Id, order.OrderCode, order.UserName, order.CreatedDate, order.Status);
            var newOrder = orderPrototype.Clone();

            // Tạo mã đơn hàng mới
            string newOrderCode = $"CLONE-{order.OrderCode}-{DateTime.Now.Ticks}";

            // Lưu đơn hàng mới vào database
            var clonedOrder = new OrderModel
            {
                OrderCode = newOrderCode,
                UserName = newOrder.UserName,
                CreatedDate = DateTime.Now,
                Status = newOrder.Status
            };

            _dataContext.Orders.Add(clonedOrder);
            await _dataContext.SaveChangesAsync();

            // Clone từng OrderDetail
            foreach (var detail in orderDetails)
            {
                var clonedDetail = new OrderDetails
                {
                    UserName = detail.UserName,
                    OrderCode = newOrderCode, // Gán mã đơn hàng mới
                    ProductId = detail.ProductId,
                    VariationId = detail.VariationId,
                    Price = detail.Price,
                    Quantity = detail.Quantity,
                    DiscountAmount = detail.DiscountAmount
                };

                _dataContext.OrderDetails.Add(clonedDetail);
            }

            await _dataContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Order cloned successfully", newOrderCode = newOrderCode });
        }

    }
}
