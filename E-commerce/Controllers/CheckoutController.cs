using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stripe.BillingPortal;
using System.Security.Claims;
using Stripe.Checkout;
using Stripe;

namespace E_commerce.Controllers
{
	public class CheckoutController : Controller
	{
		private readonly DataContext _datacontext;
		private readonly IEmailSender _emailSender;
		public CheckoutController(DataContext context, IEmailSender emailSender)
		{
			_datacontext = context;
			_emailSender = emailSender;
		}
        public async Task<IActionResult> Checkout()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                // Retrieve cart items
                List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
                // Initialize Stripe session options
                var domain = "http://localhost:5139/";
                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = domain + $"Checkout/OrderConfirmation?ordercode={Guid.NewGuid()}", // Generate unique order code
                    CancelUrl = domain + $"Cart", // Redirect to Cart on cancellation
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment"
                };
                // Add cart items to Stripe session
                foreach (var cart in cartItems)
                {
                    var sessionListItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(cart.Price * 100), // Amount in cents
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = cart.ProductName.ToString(),
                            }
                        },
                        Quantity = cart.Quantity
                    };
                    options.LineItems.Add(sessionListItem);
                }

                var shippingPriceCookie = Request.Cookies["ShippingPrice"];
                if (shippingPriceCookie != null)
                {
                    var shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
                    var shippingLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(shippingPrice * 100), // Amount in cents
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Shipping Fee",
                            }
                        },
                        Quantity = 1
                    };
                    options.LineItems.Add(shippingLineItem);
                }

                var service = new Stripe.Checkout.SessionService();
                Stripe.Checkout.Session session = service.Create(options);
                // Redirect to Stripe checkout page
                return Redirect(session.Url);
            }
        }
        public async Task<IActionResult> OrderConfirmation(string ordercode)
        {
            // Verify the Stripe payment session
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }
            // Retrieve cart items
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems == null || !cartItems.Any())
            {
                TempData["error"] = "No items in the cart to process!";
                return RedirectToAction("Cart", "Cart");
            }
            // Retrieve shipping cost from cookies
            var shippingPriceCookie = Request.Cookies["ShippingPrice"];
            decimal shippingPrice = 0;
            if (shippingPriceCookie != null)
            {
                var shippingPriceJson = shippingPriceCookie;
                shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceJson);
            }
            // Create the order
            var orderItem = new OrderModel
            {
                OrderCode = ordercode,
                ShippingCost = shippingPrice,
                UserName = userEmail,
                Status = 1, // Set the status as "pending" or "paid"
                CreatedDate = DateTime.Now
            };
            _datacontext.Add(orderItem);
            await _datacontext.SaveChangesAsync();
            // Create order details and update product stock
            foreach (var cart in cartItems)
            {
                var orderdetails = new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = ordercode,
                    ProductId = cart.ProductId,
                    Price = cart.Price,
                    Quantity = cart.Quantity
                };
                var product = await _datacontext.Products.Where(p => p.Id == cart.ProductId).FirstAsync();
                product.Quantity -= cart.Quantity;
                product.Sold += cart.Quantity;
                _datacontext.Update(product);
                _datacontext.Add(orderdetails);
            }
            await _datacontext.SaveChangesAsync();
            // Send order confirmation email
            var receiver = userEmail;
            var subject = "Order Successfully";
            var message = "We have received your order." +
                          "Your Order will be delivered to your house in 1-2 days. Thank you for your order!";
            await _emailSender.SendEmailAsync(receiver, subject, message);
            TempData["success"] = "Checkout successfully";
            // Clear cart
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index", "Cart");
        }
    }
}

