using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace E_commerce.Repository
{
    public class CouponManager
    {
        private readonly DataContext _dataContext;
        private readonly CouponFactory _factory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CouponManager> _logger;

        public CouponManager(DataContext dataContext,
                            IHttpContextAccessor httpContextAccessor,
                            ILogger<CouponManager> logger,
                            CouponFactory factory) // Nhận CouponFactory qua DI
        {
            _dataContext = dataContext;
            _factory = factory; // Sử dụng instance Singleton
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<(bool Success, string Message, decimal Discount)> ApplyCouponAsync(string couponCode, decimal orderAmount)
        {
            _logger.LogDebug("Attempting to apply coupon: {CouponCode} for order amount: {OrderAmount}", couponCode, orderAmount);

            var coupon = await _dataContext.Coupons
                .FirstOrDefaultAsync(c => c.Code == couponCode);
            if (coupon == null)
            {
                _logger.LogWarning("Coupon not found: {CouponCode}", couponCode);
                return (false, "Invalid or expired coupon code!", 0m);
            }

            if (coupon.ExpiryDate < DateTime.Now)
            {
                _logger.LogWarning("Coupon expired: {CouponCode}, ExpiryDate: {ExpiryDate}", couponCode, coupon.ExpiryDate);
                return (false, "Invalid or expired coupon code!", 0m);
            }

            if (coupon.MaxUsage.HasValue && coupon.UsedCount >= coupon.MaxUsage.Value)
            {
                _logger.LogWarning("Coupon usage limit reached: {CouponCode}, UsedCount: {UsedCount}, MaxUsage: {MaxUsage}",
                    couponCode, coupon.UsedCount, coupon.MaxUsage);
                return (false, "Invalid or expired coupon code!", 0m);
            }

            string userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("User not authenticated when applying coupon: {CouponCode}", couponCode);
                return (false, "User not authenticated!", 0m);
            }

            var hasUsed = await _dataContext.CouponUsages
                .AnyAsync(cu => cu.UserId == userId && cu.CouponCode == coupon.Code);
            if (hasUsed)
            {
                _logger.LogWarning("User {UserId} has already used coupon: {CouponCode}", userId, couponCode);
                return (false, "You have already used this coupon!", 0m);
            }

            var flyweightCoupon = _factory.GetCoupon(coupon.Code, coupon.DiscountAmount, coupon.IsPercentage, coupon.MinOrderValue);
            decimal discount = flyweightCoupon.CalculateDiscount(orderAmount);

            if (discount == 0m)
            {
                _logger.LogInformation("Coupon {CouponCode} not applied due to insufficient order amount: {OrderAmount}, MinOrderValue: {MinOrderValue}",
                    couponCode, orderAmount, coupon.MinOrderValue);
                return (false, $"Minimum order value must be ${coupon.MinOrderValue.Value} to apply this coupon!", 0m);
            }

            coupon.UsedCount++;
            _dataContext.CouponUsages.Add(new CouponUsageModel
            {
                UserId = userId,
                CouponCode = coupon.Code,
                UsedAt = DateTime.Now
            });
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Coupon {CouponCode} applied successfully for user {UserId}. Discount: {Discount}",
                couponCode, userId, discount);
            return (true, $"Coupon applied successfully! Discount: ${discount:F2}", discount);
        }
    }
}