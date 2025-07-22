using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace E_commerce.Controllers
{
    public class CouponController : Controller
    {
        // Trang quay bánh xe coupon
        [HttpGet]
        public IActionResult Spin()
        {
            return View();
        }

        // API nhận kết quả quay và trả về coupon (nếu muốn xử lý lưu coupon cho user)
        [HttpPost]
        public IActionResult Claim(string couponCode)
        {
            // TODO: Lưu couponCode cho user hiện tại (nếu cần)
            // Có thể kiểm tra đăng nhập, lưu vào DB, v.v.
            return Json(new { success = true, message = "Coupon claimed!", code = couponCode });
        }

         // Danh sách coupon đã nhận của user
        [Authorize]
        [HttpGet]
        public IActionResult MyCoupons()
        {
            // TODO: Lấy danh sách coupon từ DB theo user hiện tại
            // Ví dụ:
            // var userId = User.Identity.Name;
            // var coupons = _dbContext.UserCoupons.Where(x => x.UserName == userId).ToList();
            // return View(coupons);

            return View(); // Trả về view danh sách coupon (bạn cần tạo Views/Coupon/MyCoupons.cshtml)
        }
    }
}