namespace E_commerce.Models
{
    public class ConcreteCoupon : ICoupon
    {
        private readonly string _code;
        private readonly decimal _discountAmount;
        private readonly bool _isPercentage;
        private readonly int? _minOrderValue;

        public ConcreteCoupon(string code, decimal discountAmount, bool isPercentage, int? minOrderValue)
        {
            _code = code;
            _discountAmount = discountAmount;
            _isPercentage = isPercentage;
            _minOrderValue = minOrderValue;
        }

        public decimal CalculateDiscount(decimal orderAmount)
        {
            if (_minOrderValue.HasValue && orderAmount < _minOrderValue.Value)
            {
                return 0m; // Không đủ điều kiện áp dụng
            }
            return _isPercentage ? (orderAmount * _discountAmount / 100m) : _discountAmount;
        }
    }
}