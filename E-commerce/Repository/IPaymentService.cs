using E_commerce.Models;

namespace E_commerce.Repository
{
    public interface IPaymentService
    {
        Task<string> ProcessPayment(OrderModel order, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount);
    }

}
