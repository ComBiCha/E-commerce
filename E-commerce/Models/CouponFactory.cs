using Microsoft.Extensions.Logging;

namespace E_commerce.Models
{
    public class CouponFactory
    {
        private readonly Dictionary<string, ICoupon> _couponPool = new();
        private readonly ILogger<CouponFactory> _logger;

        public CouponFactory(ILogger<CouponFactory> logger)
        {
            _logger = logger;
        }

        public ICoupon GetCoupon(string code, decimal discountAmount, bool isPercentage, int? minOrderValue)
        {
            string key = $"{code}_{discountAmount}_{isPercentage}_{minOrderValue}";
            if (!_couponPool.TryGetValue(key, out var coupon))
            {
                coupon = new ConcreteCoupon(code, discountAmount, isPercentage, minOrderValue);
                _couponPool[key] = coupon;
                _logger.LogInformation("Created new coupon Flyweight: {Key}", key);
            }
            else
            {
                _logger.LogInformation("Reused existing coupon Flyweight: {Key}", key);
            }
            return coupon;
        }
    }
}