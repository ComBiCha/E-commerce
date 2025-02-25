using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
	[Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderController : Controller
	{
		private readonly DataContext _dataContext;
		public OrderController(DataContext context)
		{
			_dataContext = context;
		}
        public async Task<IActionResult> Index()
        {
            return View(await _dataContext.Orders.OrderByDescending(o => o.CreatedDate).ToListAsync());
        }
        /*public async Task<IActionResult> Index(int pg = 1)
        {
            List<OrderModel> order = _dataContext.Orders
                                         .OrderByDescending(o => o.CreatedDate)
                                         .ToList();


            const int pageSize = 10; //10 items/trang

            if (pg < 1) //page < 1;
            {
                pg = 1; //page ==1
            }
            int recsCount = order.Count(); //33 items;

            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize; //(3 - 1) * 10; 

            //category.Skip(20).Take(10).ToList()

            var data = order.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }*/
        public async Task<IActionResult> ViewOrder(string ordercode)
        {
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == ordercode);

            if (order == null)
            {
                return NotFound();
            }

            var userEmail = order.UserName;
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            decimal discountRate = user?.GetDiscountRate() ?? 0m; // Lấy mức giảm giá từ UserModel
            decimal productTotal = await _dataContext.OrderDetails
                .Where(o => o.OrderCode == ordercode)
                .SumAsync(o => o.Price * o.Quantity); // Tính tổng giá sản phẩm

            decimal discountAmount = productTotal * discountRate; // Số tiền giảm giá

            ViewBag.Order = order;
            ViewBag.DiscountRate = discountRate; // Gửi Discount Rate sang View
            ViewBag.DiscountAmount = discountAmount; // Số tiền giảm giá
            ViewBag.ProductTotal = productTotal;

            var DetailsOrder = await _dataContext.OrderDetails
                .Include(o => o.Product)
                    .ThenInclude(p => p.Warranty)
                .Include(o => o.Variation)
                .Include(o => o.Variation.Material)
                .Include(o => o.Variation.Color)
                .Where(o => o.OrderCode == ordercode)
                .ToListAsync();

            return View(DetailsOrder);
        }


        /*public async Task<IActionResult> Delete(int Id)
        {
            OrderController order = await _dataContext.Orders.FindAsync(Id);
            _dataContext.Orders.Remove(order);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Product removed successfully";
            return RedirectToAction("Index");
        }*/

        [HttpPost]
        [HttpPost]
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

                // Nếu đơn hàng hoàn thành, cộng điểm cho user
                if (status == 5)
                {
                    var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == order.UserName);
                    if (user != null)
                    {
                        var totalAmount = await _dataContext.OrderDetails
                            .Where(od => od.OrderCode == ordercode)
                            .SumAsync(od => od.Price * od.Quantity);

                        user.Points += Convert.ToInt32(totalAmount); // Chuyển decimal thành int gần nhất
                        await _dataContext.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, message = "Order status updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while updating the order status.", error = ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> Delete(string ordercode)
        {
            // Tìm đơn hàng theo ordercode
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);

            if (order == null)
            {
                return NotFound();  // Trả về lỗi nếu không tìm thấy order
            }

            try
            {
                // Xóa tất cả OrderDetails liên quan (nếu cần thiết)
                var orderDetails = await _dataContext.OrderDetails.Where(od => od.OrderCode == ordercode).ToListAsync();

                if (orderDetails.Any())
                {
                    _dataContext.OrderDetails.RemoveRange(orderDetails);
                }

                // Xóa Order
                _dataContext.Orders.Remove(order);
                await _dataContext.SaveChangesAsync(); // Lưu thay đổi vào DB

                TempData["success"] = "Order deleted successfully!"; // Thông báo thành công
            }
            catch (Exception ex)
            {
                // Xử lý lỗi và thông báo cho người dùng
                ModelState.AddModelError("", "An error occurred while deleting the order: " + ex.Message);
                return RedirectToAction("Index");
            }

            // Chuyển hướng về trang Index sau khi xóa thành công
            return RedirectToAction("Index");
        }

    }
}
