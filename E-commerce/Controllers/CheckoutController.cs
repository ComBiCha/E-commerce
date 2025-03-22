using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stripe.Checkout;
using System.Security.Claims;
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

            var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (!cartItems.Any())
            {
                TempData["error"] = "Your cart is empty!";
                return RedirectToAction("Index", "Cart");
            }

            decimal discountAmount2 = 0;
            var discountAmountSession = HttpContext.Session.GetString("DiscountAmount");
            if (!string.IsNullOrEmpty(discountAmountSession))
            {
                discountAmount2 = decimal.Parse(discountAmountSession);
            }

            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
            decimal discountRate = user?.GetDiscountRate() ?? 0m;
            decimal discountAmount = grandTotal * discountRate;
            decimal finalTotal = grandTotal - discountAmount - discountAmount2;

			var shippingPriceCookie = Request.Cookies["ShippingPrice"];
			decimal shippingPrice = 0;
			if (shippingPriceCookie != null)
			{
				shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
			}

			var order = new OrderModel
			{
				OrderCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(), // Tạo mã đơn hàng
				CreatedDate = DateTime.Now
			};

			var paymentMethod = HttpContext.Session.GetString("PaymentMethod") ?? "stripe";
			Console.WriteLine($"Payment Method: {paymentMethod}");

			var paymentService = PaymentServiceFactory.GetPaymentService(paymentMethod, _datacontext);
            var redirectUrl = await paymentService.ProcessPayment(order, cartItems, shippingPrice, discountAmount2+discountAmount);

            return Redirect(redirectUrl);
        }

		[HttpPost]
		public async Task<IActionResult> Checkout(string paymentMethod, string guestEmail)
		{
			// Kiểm tra tài khoản đăng nhập
			var userEmail = User.FindFirstValue(ClaimTypes.Email);

			// Nếu không có tài khoản đăng nhập, lấy email từ form nhập của khách
			if (userEmail == null)
			{
				if (string.IsNullOrEmpty(guestEmail))
				{
					TempData["error"] = "Please enter your email to proceed!";
                    return RedirectToAction("Index", "Cart");
                }
				userEmail = guestEmail;
			}

			// Lấy thông tin giỏ hàng
			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			if (!cartItems.Any())
			{
				TempData["error"] = "Your cart is empty!";
				return RedirectToAction("Index", "Cart");
			}

			// Lấy thông tin giảm giá và phí vận chuyển
			decimal discountAmount2 = 0;
			var discountAmountSession = HttpContext.Session.GetString("DiscountAmount");
			if (!string.IsNullOrEmpty(discountAmountSession))
			{
				discountAmount2 = decimal.Parse(discountAmountSession);
			}

			decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
			decimal discountRate = 0m;
			if (User.Identity.IsAuthenticated)
			{
				var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
				discountRate = user?.GetDiscountRate() ?? 0m;
			}
			decimal discountAmount = grandTotal * discountRate;
			decimal finalTotal = grandTotal - discountAmount - discountAmount2;

			var shippingPriceCookie = Request.Cookies["ShippingPrice"];
			decimal shippingPrice = shippingPriceCookie != null ? JsonConvert.DeserializeObject<decimal>(shippingPriceCookie) : 0;

			// Lưu phương thức thanh toán
			paymentMethod ??= "stripe";
			HttpContext.Session.SetString("PaymentMethod", paymentMethod);

			// Tạo đơn hàng
			var order = new OrderModel
			{
				OrderCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(),
				CreatedDate = DateTime.Now,
				UserName = userEmail // Lưu email của tài khoản hoặc khách
			};

			var paymentService = PaymentServiceFactory.GetPaymentService(paymentMethod, _datacontext);
			var redirectUrl = await paymentService.ProcessPayment(order, cartItems, shippingPrice, discountAmount2 + discountAmount);

			return Redirect(redirectUrl);
		}


		//Order 
		public async Task<IActionResult> OrderConfirmation(string ordercode)
        {
			Console.WriteLine($"OrderCode received: {ordercode}");
            var order = await _datacontext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            if (order == null)
            {
                return RedirectToAction("Index", "Cart");
            }
            string userEmail = order.UserName;
            var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (!cartItems.Any())
            {
                TempData["error"] = "No items in the cart to process!";
                return RedirectToAction("Index", "Cart");
            }

            decimal shippingPrice = 0;
            var shippingPriceCookie = Request.Cookies["ShippingPrice"];
            if (shippingPriceCookie != null)
            {
                shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
            }

            decimal discountAmount2 = 0;
            var discountAmountSession = HttpContext.Session.GetString("DiscountAmount");
            if (!string.IsNullOrEmpty(discountAmountSession))
            {
                discountAmount2 = decimal.Parse(discountAmountSession);
            }

            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
            decimal discountRate = user?.GetDiscountRate() ?? 0m;
            decimal discountAmount = grandTotal * discountRate;
            decimal finalTotal = grandTotal - discountAmount - discountAmount2 + shippingPrice;
            //sv sua
            
            if (order != null)
            {
                order.ShippingCost = shippingPrice;
                order.Address = HttpContext.Session.GetString("ShippingAddress") ?? order.Address;
                order.Status = 1; // Cập nhật trạng thái đơn hàng thành "Chờ xác nhận"
                order.PaymentIntentId = HttpContext.Session.GetString("StripeSessionId") ?? order.PaymentIntentId;

                _datacontext.Orders.Update(order);
                await _datacontext.SaveChangesAsync();
            }
            else
            {
                var order1 = new OrderBuilder()
                .SetOrderCode(ordercode)
                .SetShippingCost(shippingPrice)
                .SetAddress(HttpContext.Session.GetString("ShippingAddress"))
                .SetUserName(userEmail)
                .SetCreatedDate(DateTime.Now)
                .SetStatus(1)
                .SetPaymentIntentId(HttpContext.Session.GetString("StripeSessionId"))
                .Build();
                _datacontext.Orders.Add(order1);
                await _datacontext.SaveChangesAsync();
            }
            foreach (var cart in cartItems)
            {
                var variation = await _datacontext.Variations
                    .Include(v => v.Material)
                    .Include(v => v.Color)
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.Id == cart.VariationId);

                if (variation == null)
                {
                    continue;
                }

                decimal itemTotal = cart.Quantity * cart.Price;
                decimal itemDiscount = itemTotal * discountRate;
                decimal itemDiscountFromCoupon = (itemTotal / grandTotal) * discountAmount2;
                decimal totalDiscountForItem = itemDiscount + itemDiscountFromCoupon;

                var orderDetail = new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = ordercode,
                    ProductId = variation.ProductId,
                    VariationId = variation.Id,
                    Price = cart.Price,
                    Quantity = cart.Quantity,
                    DiscountAmount = totalDiscountForItem
                };
                _datacontext.OrderDetails.Add(orderDetail);

                if (variation.Product.WarrantyPeriod > 0)
                {
                    var warranty = new WarrantyModel
                    {
                        WarrantyCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(),
                        ProductId = variation.ProductId,
                        VariationId = variation.Id,
                        OrderCode = ordercode,
                        ExpirationDate = DateTime.Now.AddMonths(variation.Product.WarrantyPeriod),
                        CreatedDate = DateTime.Now
                    };
                    _datacontext.Warranties.Add(warranty);
                }

                variation.Stock -= cart.Quantity;
                variation.Product.Sold += cart.Quantity;
                variation.Product.Quantity -= cart.Quantity;
                _datacontext.Update(variation);
                _datacontext.Update(variation.Product);
            }

            var code = HttpContext.Session.GetString("CouponCode");
            if (!string.IsNullOrEmpty(code))
            {
                var coupon = await _datacontext.Coupons.FirstOrDefaultAsync(c => c.Code == code);
                if (coupon != null)
                {
                    coupon.UsedCount += 1;
                    _datacontext.Update(coupon);
                }
            }
            await _datacontext.SaveChangesAsync();

			var orderDetails = await _datacontext.OrderDetails
				.Where(od => od.OrderCode == ordercode)
				.Include(od => od.Product)
				.Include(od => od.Variation)
				.ToListAsync();

			var emailBody = new StringBuilder();
			emailBody.AppendLine("Dear Customer,");
			emailBody.AppendLine("We have successfully received your order. Here are the details:");
			emailBody.AppendLine();

			emailBody.AppendLine($"**Order Code:** {ordercode}");
			emailBody.AppendLine($"**Order Date:** {DateTime.Now:yyyy-MM-dd}");
			emailBody.AppendLine($"**Shipping Cost:** ${shippingPrice:F2}");
			emailBody.AppendLine($"**Discount Applied (membership):** -${discountAmount:F2}");
			emailBody.AppendLine($"**Discount Applied (coupon code):** -${discountAmount2:F2}");
			emailBody.AppendLine($"**Final Total:** ${finalTotal:F2}");
			emailBody.AppendLine($"**Total Items:** {cartItems.Count}");
			emailBody.AppendLine();

			var baseUrl = "http://sangrk-001-site1.ptempurl.com/";
			emailBody.AppendLine("**Products in Your Order:**");
			foreach (var detail in orderDetails)
			{
				var productImageUrl = $"{baseUrl}/media/variations/{detail.Variation.ImageUrl}";
				var materialName = detail.Variation?.Material?.Name ?? "Unknown Material";
				var colorName = detail.Variation?.Color?.Name ?? "Unknown Color";
				var size = detail.Variation?.Size ?? 1;
				emailBody.AppendLine($"- **Product Name**: {detail.Product.Name} ({materialName} - {colorName} - Size:{size})");
				emailBody.AppendLine($"  - Quantity: {detail.Quantity}");
				emailBody.AppendLine($"  - Price per Unit: ${detail.Price:F2}");
				emailBody.AppendLine($"  - Total: ${detail.Quantity * detail.Price:F2}");
				emailBody.AppendLine($"  - Product Image: [View Image]({productImageUrl})");
				emailBody.AppendLine();
			}

			emailBody.AppendLine("Thank you for shopping with us!");
			emailBody.AppendLine("If you have any questions, feel free to contact our support team.");
			emailBody.AppendLine();
			emailBody.AppendLine("Best regards");

			await _emailSender.SendEmailAsync(userEmail, "Order Successfully", emailBody.ToString());

			HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("DiscountAmount");
            HttpContext.Session.Remove("CouponCode");
            HttpContext.Session.Remove("ShippingAddress");

            return View(order);
        }

        public async Task<IActionResult> OrderConfirmationStripe(string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                Console.WriteLine("DEBUG: session_id is null or empty.");
                return RedirectToAction("Index", "Cart");
            }

            var service = new Stripe.Checkout.SessionService();
            var session = service.Get(session_id);

            if (session == null)
            {
                Console.WriteLine("DEBUG: Stripe session is null.");
                return RedirectToAction("Index", "Cart");
            }
            if (!session.Metadata.ContainsKey("ordercode"))
            {
                Console.WriteLine("DEBUG: Stripe session does not contain ordercode.");
                return RedirectToAction("Index", "Cart");
            }
            string ordercode = session.Metadata["ordercode"];
                // Tìm đơn hàng trong database
                var order = await _datacontext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
                if (order != null)
                {
                    order.Status = 1; // Cập nhật trạng thái đã thanh toán
                    order.PaymentIntentId = session.PaymentIntentId;
                    order.CreatedDate = DateTime.UtcNow;

                    _datacontext.Orders.Update(order);
                    await _datacontext.SaveChangesAsync();
                }

            string userEmail = order.UserName;
            var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (!cartItems.Any())
            {
                TempData["error"] = "No items in the cart to process!";
                return RedirectToAction("Index", "Cart");
            }

            decimal shippingPrice = 0;
            var shippingPriceCookie = Request.Cookies["ShippingPrice"];
            if (shippingPriceCookie != null)
            {
                shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
            }

            decimal discountAmount2 = 0;
            var discountAmountSession = HttpContext.Session.GetString("DiscountAmount");
            if (!string.IsNullOrEmpty(discountAmountSession))
            {
                discountAmount2 = decimal.Parse(discountAmountSession);
            }

            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
            decimal discountRate = user?.GetDiscountRate() ?? 0m;
            decimal discountAmount = grandTotal * discountRate;
            decimal finalTotal = grandTotal - discountAmount - discountAmount2 + shippingPrice;
            //sv sua

            if (order != null)
            {
                order.ShippingCost = shippingPrice;
                order.Address = HttpContext.Session.GetString("ShippingAddress") ?? order.Address;
                order.Status = 1; // Cập nhật trạng thái đơn hàng thành "Chờ xác nhận"
                order.PaymentIntentId = HttpContext.Session.GetString("StripeSessionId") ?? order.PaymentIntentId;

                _datacontext.Orders.Update(order);
                await _datacontext.SaveChangesAsync();
            }
            else
            {
                var order1 = new OrderBuilder()
                .SetOrderCode(ordercode)
                .SetShippingCost(shippingPrice)
                .SetAddress(HttpContext.Session.GetString("ShippingAddress"))
                .SetUserName(userEmail)
                .SetCreatedDate(DateTime.Now)
                .SetStatus(1)
                .SetPaymentIntentId(HttpContext.Session.GetString("StripeSessionId"))
                .Build();
                _datacontext.Orders.Add(order1);
                await _datacontext.SaveChangesAsync();
            }
            foreach (var cart in cartItems)
            {
                var variation = await _datacontext.Variations
                    .Include(v => v.Material)
                    .Include(v => v.Color)
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.Id == cart.VariationId);

                if (variation == null)
                {
                    continue;
                }

                decimal itemTotal = cart.Quantity * cart.Price;
                decimal itemDiscount = itemTotal * discountRate;
                decimal itemDiscountFromCoupon = (itemTotal / grandTotal) * discountAmount2;
                decimal totalDiscountForItem = itemDiscount + itemDiscountFromCoupon;

                var orderDetail = new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = ordercode,
                    ProductId = variation.ProductId,
                    VariationId = variation.Id,
                    Price = cart.Price,
                    Quantity = cart.Quantity,
                    DiscountAmount = totalDiscountForItem
                };
                _datacontext.OrderDetails.Add(orderDetail);

                if (variation.Product.WarrantyPeriod > 0)
                {
                    var warranty = new WarrantyModel
                    {
                        WarrantyCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(),
                        ProductId = variation.ProductId,
                        VariationId = variation.Id,
                        OrderCode = ordercode,
                        ExpirationDate = DateTime.Now.AddMonths(variation.Product.WarrantyPeriod),
                        CreatedDate = DateTime.Now
                    };
                    _datacontext.Warranties.Add(warranty);
                }

                variation.Stock -= cart.Quantity;
                variation.Product.Sold += cart.Quantity;
                variation.Product.Quantity -= cart.Quantity;
                _datacontext.Update(variation);
                _datacontext.Update(variation.Product);
            }

            var code = HttpContext.Session.GetString("CouponCode");
            if (!string.IsNullOrEmpty(code))
            {
                var coupon = await _datacontext.Coupons.FirstOrDefaultAsync(c => c.Code == code);
                if (coupon != null)
                {
                    coupon.UsedCount += 1;
                    _datacontext.Update(coupon);
                }
            }
            await _datacontext.SaveChangesAsync();

            var orderDetails = await _datacontext.OrderDetails
                .Where(od => od.OrderCode == ordercode)
                .Include(od => od.Product)
                .Include(od => od.Variation)
                .ToListAsync();

            var baseUrl = "http://sangrk-001-site1.ptempurl.com/";
            var emailBody = new StringBuilder();
            emailBody.AppendLine("Dear Customer,");
            emailBody.AppendLine("We have successfully received your order. Here are the details:");
            emailBody.AppendLine();

            emailBody.AppendLine($"**Order Code:** {ordercode}");
            emailBody.AppendLine($"**Order Date:** {DateTime.Now:yyyy-MM-dd}");
            emailBody.AppendLine($"**Shipping Cost:** ${shippingPrice:F2}");
            emailBody.AppendLine($"**Discount Applied (membership):** -${discountAmount:F2}");
            emailBody.AppendLine($"**Discount Applied (coupon code):** -${discountAmount2:F2}");
            emailBody.AppendLine($"**Final Total:** ${finalTotal:F2}");
            emailBody.AppendLine($"**Total Items:** {cartItems.Count}");
            string orderLink = $"{baseUrl}Account/ViewOrder?orderCode={order.OrderCode}";
            emailBody.AppendLine($"Bạn có thể xem đơn hàng của mình tại đây: {orderLink}");
            emailBody.AppendLine();


            emailBody.AppendLine("**Products in Your Order:**");
            foreach (var detail in orderDetails)
            {
                var productImageUrl = $"{baseUrl}/media/variations/{detail.Variation.ImageUrl}";
                var materialName = detail.Variation?.Material?.Name ?? "Unknown Material";
                var colorName = detail.Variation?.Color?.Name ?? "Unknown Color";
                var size = detail.Variation?.Size ?? 1;
                emailBody.AppendLine($"- **Product Name**: {detail.Product.Name} ({materialName} - {colorName} - Size:{size})");
                emailBody.AppendLine($"  - Quantity: {detail.Quantity}");
                emailBody.AppendLine($"  - Price per Unit: ${detail.Price:F2}");
                emailBody.AppendLine($"  - Total: ${detail.Quantity * detail.Price:F2}");
                emailBody.AppendLine($"  - Product Image: [View Image]({productImageUrl})");
                emailBody.AppendLine();
            }

            emailBody.AppendLine("Thank you for shopping with us!");
            emailBody.AppendLine("If you have any questions, feel free to contact our support team.");
            emailBody.AppendLine();
            emailBody.AppendLine("Best regards");

            try
            {
                await _emailSender.SendEmailAsync(userEmail, "Order Successfully", emailBody.ToString());
                Console.WriteLine("DEBUG: Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to send email. {ex.Message}");
            }


            HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("DiscountAmount");
            HttpContext.Session.Remove("CouponCode");
            HttpContext.Session.Remove("ShippingAddress");

            return View(order);
        }

    }
}

