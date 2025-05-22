namespace E_commerce.Models
{
    public interface ICoupon
    {
        decimal CalculateDiscount(decimal orderAmount);
    }
}