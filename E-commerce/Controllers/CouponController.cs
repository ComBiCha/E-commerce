using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using E_commerce.Areas.Admin.Repository;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using E_commerce.Models;

namespace E_commerce.Controllers
{
    public class CouponController : Controller
    {
        public readonly DataContext _dataContext;

        public CouponController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        // Trang quay bánh xe coupon
        [HttpGet]
        public IActionResult Spin()
        {
            return View();
        }

        // API nhận kết quả quay và trả về coupon (lưu vào UserVoucher)
        [HttpPost]
        public IActionResult Claim([FromBody] ClaimRequest req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Lưu voucher mới nhận được vào UserVoucher
            var userVoucher = new UserVoucher
            {
                UserId = userId,
                CouponCode = req.couponCode,
                ReceivedAt = DateTime.Now,
                IsUsed = false
            };
            _dataContext.UserVouchers.Add(userVoucher);
            _dataContext.SaveChanges();

            return Json(new { success = true });
        }

        // API lấy danh sách voucher đã nhận (kho voucher của user)
        [HttpGet]
        public IActionResult MyVouchers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vouchers = _dataContext.UserVouchers
                .Where(uv => uv.UserId == userId && !uv.IsUsed)
                .OrderByDescending(uv => uv.ReceivedAt)
                .ToList();
            return Json(vouchers);
        }

        [HttpGet]
        public IActionResult GetAvailableCoupons()
        {
            var coupons = _dataContext.Coupons
                .Where(c => c.ExpiryDate > DateTime.Now && (c.MaxUsage == null || c.UsedCount < c.MaxUsage))
                .Select(c => c.Code)
                .ToList();

            if (coupons == null) coupons = new List<string>();

            return Json(coupons);
        }

        [HttpGet]
        public IActionResult GetCouponDetail(string code)
        {
            var coupon = _dataContext.Coupons.FirstOrDefault(c => c.Code == code);
            if (coupon == null) return NotFound();
            return Json(coupon);
        }

        // Trong CouponController.cs
        [HttpGet]
        public IActionResult GetSpinCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _dataContext.Users.FirstOrDefault(u => u.Id == userId);
            return Json(new { spinCount = user?.SpinCount ?? 0 });
        }
    }
}