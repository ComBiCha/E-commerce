using E_commerce.Models;
using Stripe.Checkout;

namespace E_commerce.Repository
{
    public class StripePaymentService : IPaymentService
    {
        private readonly DataContext _datacontext;

        public StripePaymentService(DataContext datacontext)
        {
            _datacontext = datacontext;
        }

        public async Task<string> ProcessPayment(OrderModel order, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount)
        {
            var domain = "http://localhost:5139/";

            // **1. Lưu order vào database trước khi gọi Stripe**
            order.Status = 0; // Trạng thái chờ thanh toán
            order.CreatedDate = DateTime.UtcNow;

            _datacontext.Orders.Add(order);
            await _datacontext.SaveChangesAsync();  // Lưu vào DB ngay lập tức

            // **2. Khởi tạo session cho Stripe**
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = domain + "Checkout/OrderConfirmationStripe?session_id={CHECKOUT_SESSION_ID}&orderCode=" + order.OrderCode,
                CancelUrl = domain + "Cart",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
                Metadata = new Dictionary<string, string>
        {
            { "ordercode", order.OrderCode } // Lưu ordercode vào metadata để truy xuất sau này
        }
            };

            foreach (var cart in cartItems)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(cart.Price * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = cart.ProductName
                        }
                    },
                    Quantity = cart.Quantity
                });
            }

            if (shippingPrice > 0)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(shippingPrice * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Shipping Fee"
                        }
                    },
                    Quantity = 1
                });
            }

            if (discountAmount > 0)
            {
                var couponService = new Stripe.CouponService();
                var coupon = couponService.Create(new Stripe.CouponCreateOptions
                {
                    AmountOff = (long)(discountAmount * 100),
                    Currency = "usd",
                    Duration = "once"
                });

                options.Discounts = new List<SessionDiscountOptions>
        {
            new SessionDiscountOptions { Coupon = coupon.Id }
        };
            }

            var service = new Stripe.Checkout.SessionService();
            Stripe.Checkout.Session session = service.Create(options);

            return session.Url;
        }


    }

}
