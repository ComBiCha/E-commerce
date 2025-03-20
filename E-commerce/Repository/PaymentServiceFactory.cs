using E_commerce.Services;
using PayPal.Api;

namespace E_commerce.Repository
{
    public class PaymentServiceFactory
    {
        public static IPaymentService GetPaymentService(string paymentMethod, DataContext context)
        {
            return paymentMethod.ToLower() switch
            {
                "stripe" => new StripePaymentService(context),
                "cod" => new CODPaymentService(context),
                "paypal" => new PayPalPaymentAdapter(new PayPalSdk(), context),
                _ => throw new NotImplementedException("Payment method not supported")
            };
        }
    }
}
