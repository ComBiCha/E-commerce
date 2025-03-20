namespace E_commerce.Models
{
    public class CouponUsageModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int CouponId { get; set; }
        public DateTime UsedAt { get; set; }
    }
}