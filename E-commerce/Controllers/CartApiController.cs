using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace E_commerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartApiController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly CouponManager _couponManager;
        private readonly UserManager<AppUserModel> _userManager;

        public CartApiController(DataContext dataContext, CouponManager couponManager, UserManager<AppUserModel> userManager)
        {
            _dataContext = dataContext;
            _couponManager = couponManager;
            _userManager = userManager;
        }

        [HttpPost("GetShippingPrice")]
        public async Task<IActionResult> GetShippingPrice([FromBody] ShippingPriceRequest request)
        {
            try
            {
                var existingShipping = await _dataContext.Shippings.FirstOrDefaultAsync(x =>
                    x.City == request.tinh &&
                    x.District == request.quan &&
                    x.Ward == request.phuong);

                decimal shippingPrice = existingShipping?.Price ?? 5;

                return Ok(new { shippingPrice });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 🔹 API ÁP DỤNG COUPON - YÊU CẦU USER EMAIL TRONG REQUEST
        [HttpPost("ApplyCoupon")]
        public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request)
        {
            try
            {
                Console.WriteLine($"ApplyCoupon called - CouponCode: {request.CouponCode}, GrandTotal: {request.GrandTotal}, UserEmail: {request.UserEmail}");

                if (string.IsNullOrEmpty(request.CouponCode))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Coupon code is required"
                    });
                }

                if (string.IsNullOrEmpty(request.UserEmail))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "User email is required"
                    });
                }

                if (request.GrandTotal <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid grand total"
                    });
                }

                // Tìm user
                var user = await _userManager.FindByEmailAsync(request.UserEmail);
                if (user == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "User not found",
                        discountAmount = 0
                    });
                }

                // Tìm coupon trong database
                var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode);

                if (coupon == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Coupon code not found",
                        discountAmount = 0
                    });
                }

                // Kiểm tra coupon còn hiệu lực
                if (coupon.ExpiryDate < DateTime.Now)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Coupon has expired",
                        discountAmount = 0
                    });
                }

                // Kiểm tra số lượng sử dụng tổng
                if (coupon.MaxUsage > 0 && coupon.UsedCount >= coupon.MaxUsage)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Coupon usage limit exceeded",
                        discountAmount = 0
                    });
                }

                // 🔹 KIỂM TRA USER ĐÃ DÙNG COUPON NÀY CHƯA
                var userCouponUsage = await _dataContext.CouponUsages
                    .FirstOrDefaultAsync(ucu => ucu.UserId == user.Id && ucu.CouponId == coupon.Id);

                if (userCouponUsage != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "You have already used this coupon",
                        discountAmount = 0
                    });
                }

                // Kiểm tra minimum order amount
                if (coupon.MinOrderValue > 0 && (decimal)request.GrandTotal < coupon.MinOrderValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Minimum order amount is ${coupon.MinOrderValue}",
                        discountAmount = 0
                    });
                }

                // Tính discount amount
                decimal discountAmount = 0;
                if (coupon.IsPercentage == true)
                {
                    discountAmount = (decimal)request.GrandTotal * (coupon.DiscountAmount / 100);
                }
                else // fixed amount
                {
                    discountAmount = coupon.DiscountAmount;

                    // Đảm bảo discount không vượt quá tổng tiền
                    if (discountAmount > (decimal)request.GrandTotal)
                    {
                        discountAmount = (decimal)request.GrandTotal;
                    }
                }

                Console.WriteLine($"ApplyCoupon success - DiscountAmount: {discountAmount}");

                return Ok(new
                {
                    success = true,
                    message = $"Coupon applied successfully! You saved ${discountAmount:F2}",
                    discountAmount = discountAmount,
                    couponCode = request.CouponCode,
                    couponId = coupon.Id // Trả về để lưu khi checkout
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ApplyCoupon error: {ex.Message}");
                return BadRequest(new
                {
                    success = false,
                    message = $"Error applying coupon: {ex.Message}"
                });
            }
        }

        // 🔹 API XÓA COUPON
        [HttpPost("RemoveCoupon")]
        public IActionResult RemoveCoupon()
        {
            try
            {
                return Ok(new
                {
                    success = true,
                    message = "Coupon removed successfully",
                    discountAmount = 0,
                    couponCode = "",
                    couponId = 0
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Error removing coupon: {ex.Message}"
                });
            }
        }
    }

    // 🔹 CẬP NHẬT REQUEST MODEL
    public class ApplyCouponRequest
    {
        public string CouponCode { get; set; }
        public double GrandTotal { get; set; }
        public string UserEmail { get; set; } // 🔹 THÊM USER EMAIL
    }

    public class ShippingPriceRequest
    {
        public string tinh { get; set; }
        public string quan { get; set; }
        public string phuong { get; set; }
        public string detailAddress { get; set; }
    }
}