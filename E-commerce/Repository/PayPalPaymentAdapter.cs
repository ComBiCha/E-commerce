using E_commerce.Models;
using E_commerce.Services;
using PayPal.Api;

namespace E_commerce.Repository
{
    public class PayPalPaymentAdapter : IPaymentService
    {
        private readonly APIContext _apiContext;
        private readonly DataContext _dbContext;

        public PayPalPaymentAdapter(PayPalSdk payPalSdk, DataContext dbContext)
        {
            _apiContext = payPalSdk.GetAPIContext();
            _dbContext = dbContext;
        }

        public async Task<string> ProcessPayment(OrderModel order, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount)
        {
            decimal totalAmount = cartItems.Sum(x => x.Quantity * x.Price) + shippingPrice - discountAmount;
            string orderCode = order.OrderCode;
            var domain = "http://localhost:5139/";  

            var payment = new Payment
            {
                intent = "sale",
                payer = new Payer { payment_method = "paypal" },
                transactions = new List<Transaction>
        {
            new Transaction
            {
                amount = new Amount
                {
                    total = totalAmount.ToString("F2"),
                    currency = "USD"
                },
                description = $"Order {orderCode}"
            }
        },
                redirect_urls = new RedirectUrls
                {
                    return_url = domain + $"Checkout/OrderConfirmation?ordercode={order.OrderCode}",
                    cancel_url = domain + "Cart",
                }
            };

            var createdPayment = payment.Create(_apiContext);
            string paymentId = createdPayment.id; // Lấy Payment ID từ PayPal

            // 🔹 Lưu Payment ID vào Order
            order.PaymentIntentId = paymentId;

            // 🔹 Cập nhật vào database
            _dbContext.Orders.Update(order);
            await _dbContext.SaveChangesAsync();

            var redirectUrl = createdPayment.links.FirstOrDefault(l => l.rel == "approval_url")?.href;
            return redirectUrl ?? throw new Exception("Không tìm thấy URL chuyển hướng PayPal");
        }

    }
}
