namespace E_commerce.Models
{
    public class CouponUsageModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string CouponCode { get; set; }
        public DateTime UsedAt { get; set; }
    }
        public class ClaimRequest
    {
        public string couponCode { get; set; }
    }
}