public class UserVoucher
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string CouponCode { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsUsed { get; set; } = false;
}

