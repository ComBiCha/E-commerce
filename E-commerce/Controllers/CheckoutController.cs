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
using System.Text;

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
			// shipping address
			var shippingAddress = HttpContext.Session.GetString("ShippingAddress");

			// Create the order
			var orderItem = new OrderModel
            {
                OrderCode = ordercode,
                ShippingCost = shippingPrice,
                Address = shippingAddress,
                UserName = userEmail,
                Status = 1, // Set the status as "pending" or "paid"
                CreatedDate = DateTime.Now
            };
            _datacontext.Add(orderItem);
            await _datacontext.SaveChangesAsync();
            // Create order details and update product stock

            /*foreach (var cart in cartItems)
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
            await _datacontext.SaveChangesAsync();*/

            foreach (var cart in cartItems)
            {
                // Thêm chi tiết đơn hàng
                var orderdetails = new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = ordercode,
                    ProductId = cart.ProductId,
                    Price = cart.Price,
                    Quantity = cart.Quantity
                };
                _datacontext.Add(orderdetails);

                // Cập nhật số lượng sản phẩm trong kho
                var product = await _datacontext.Products.Where(p => p.Id == cart.ProductId).FirstAsync();
                product.Quantity -= cart.Quantity;
                product.Sold += cart.Quantity;
                _datacontext.Update(product);

                // Tạo mã bảo hành
                var warrantyCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(); // Mã bảo hành ngẫu nhiên
                var warranty = new WarrantyModel
                {
                    WarrantyCode = warrantyCode,
                    ProductId = cart.ProductId,
                    OrderCode = ordercode,
                    ExpirationDate = DateTime.Now.AddMonths(product.WarrantyPeriod), // Lấy thời hạn bảo hành từ sản phẩm
                    CreatedDate = DateTime.Now
                };
                _datacontext.Add(warranty);
            }
            await _datacontext.SaveChangesAsync();


            // Send order confirmation email
            var receiver = userEmail;
            var subject = "Order Successfully";

            /*var warranties = await _datacontext.Warranties
            .Where(w => w.OrderCode == ordercode)
            .Include(w => w.Product)
            .ToListAsync();

            var warrantyDetails = string.Join("\n", warranties.Select(w =>
                $"Product: {w.Product.Name}, Warranty Code: {w.WarrantyCode}, Expiration Date: {w.ExpirationDate:yyyy-MM-dd}"));

            var message = $"We have received your order.\n" +
                          "Your order will be delivered to your house in 1-2 days. Thank you for your order!\n\n" +
                          "Warranty Information:\n" + warrantyDetails;

            await _emailSender.SendEmailAsync(receiver, subject, message);*/

            var warranties = await _datacontext.Warranties
            .Where(w => w.OrderCode == ordercode)
            .Include(w => w.Product)
            .ToListAsync();

            var orderDetails = await _datacontext.OrderDetails
                .Where(od => od.OrderCode == ordercode)
                .Include(od => od.Product)
                .ToListAsync();

            // Xây dựng nội dung email chi tiết
            var emailBody = new StringBuilder();
            emailBody.AppendLine("Dear Customer,");
            emailBody.AppendLine("We have successfully received your order. Here are the details:");
            emailBody.AppendLine();

            // Thông tin đơn hàng
            emailBody.AppendLine("**Order Details:**");
            emailBody.AppendLine($"Order Code: {ordercode}");
            emailBody.AppendLine($"Order Date: {DateTime.Now:yyyy-MM-dd}");
            emailBody.AppendLine($"Shipping Cost: ${shippingPrice:F2}");
            emailBody.AppendLine($"Total Items: {cartItems.Count}");
            emailBody.AppendLine();

            // Thông tin sản phẩm
            var baseUrl = "http://localhost:5139/";
            emailBody.AppendLine("**Products in Your Order:**");
            foreach (var detail in orderDetails)
            {
                var productImageUrl = $"{baseUrl}/media/products/{detail.Product.Image}";
                emailBody.AppendLine($"- **Product Name**: {detail.Product.Name}");
                emailBody.AppendLine($"  - Quantity: {detail.Quantity}");
                emailBody.AppendLine($"  - Price per Unit: ${detail.Price:F2}");
                emailBody.AppendLine($"  - Total: ${detail.Quantity * detail.Price:F2}");
                emailBody.AppendLine($"  - Product Image: [View Image]({productImageUrl})");
                emailBody.AppendLine();
            }

            // Thông tin bảo hành
            /*emailBody.AppendLine("**Warranty Information:**");
            foreach (var warranty in warranties)
            {
                emailBody.AppendLine($"- **Product Name**: {warranty.Product.Name}");
                emailBody.AppendLine($"  - Warranty Code: {warranty.WarrantyCode}");
                emailBody.AppendLine($"  - Expiration Date: {warranty.ExpirationDate:yyyy-MM-dd}");
                emailBody.AppendLine();
            }*/

            // Kết thúc email
            emailBody.AppendLine("Thank you for shopping with us!");
            emailBody.AppendLine("If you have any questions, feel free to contact our support team.");
            emailBody.AppendLine();
            emailBody.AppendLine("Best regards");

            // Gửi email
            await _emailSender.SendEmailAsync(receiver, subject, emailBody.ToString());


            TempData["success"] = "Checkout successfully";
            // Clear cart
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index", "Cart");
        }
    }
}

