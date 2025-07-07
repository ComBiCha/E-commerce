using E_commerce.Models;

namespace E_commerce.Repository
{
    public class CODPaymentService : IPaymentService
    {
        private readonly DataContext _datacontext;

        public CODPaymentService(DataContext datacontext)
        {
            _datacontext = datacontext;
        }

        public async Task<string> ProcessPayment(OrderModel order, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount)
        {
            // Đánh dấu đơn hàng đã được đặt với phương thức thanh toán là COD
            order.Status = 1; // 1: Đơn hàng chờ xác nhận
            order.PaymentIntentId = "COD";

            _datacontext.Orders.Add(order);
            await _datacontext.SaveChangesAsync();
			return "/Checkout/OrderConfirmation?ordercode=" + order.OrderCode;
        }
    }

}
