using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stripe;
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
		[HttpGet, HttpPost]
		public async Task<IActionResult> OrderConfirmation(string ordercode)
        {
            Console.WriteLine($"OrderCode received: {ordercode}");

            var order = await _datacontext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new();

            if (!cartItems.Any())
            {
                TempData["error"] = "No items in the cart to process!";
                return RedirectToAction("Index", "Cart");
            }

            string userEmail = order?.UserName ?? HttpContext.Session.GetString("UserEmail");
            var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
            {
                TempData["error"] = "User not found!";
                return RedirectToAction("Index", "Cart");
            }

            decimal shippingPrice = 0;
            if (Request.Cookies["ShippingPrice"] is string shippingPriceCookie)
                shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);

            decimal discountAmountCoupon = 0;
            if (HttpContext.Session.GetString("DiscountAmount") is string discountSession)
                discountAmountCoupon = decimal.Parse(discountSession);

            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
            decimal discountRate = user?.GetDiscountRate() ?? 0m;
            decimal discountAmountMember = grandTotal * discountRate;
            decimal finalTotal = grandTotal - discountAmountMember - discountAmountCoupon + shippingPrice;

            await using var transaction = await _datacontext.Database.BeginTransactionAsync();

            try
            {
                if (order != null)
                {
                    order.ShippingCost = shippingPrice;
                    order.Address = HttpContext.Session.GetString("ShippingAddress") ?? order.Address;
                    order.Status = 1;
                    order.PaymentIntentId = HttpContext.Session.GetString("StripeSessionId") ?? order.PaymentIntentId;
                    _datacontext.Orders.Update(order);
                }
                else
                {
                    order = new OrderBuilder()
                        .SetOrderCode(ordercode)
                        .SetShippingCost(shippingPrice)
                        .SetAddress(HttpContext.Session.GetString("ShippingAddress"))
                        .SetUserName(userEmail)
                        .SetCreatedDate(DateTime.Now)
                        .SetStatus(1)
                        .SetPaymentIntentId(HttpContext.Session.GetString("StripeSessionId"))
                        .Build();

                    _datacontext.Orders.Add(order);
                }

                foreach (var cart in cartItems)
                {
					var variation = await _datacontext.Variations
	                    .Include(v => v.Material)
	                    .Include(v => v.Color)
	                    .Include(v => v.Product)
	                    .Include(v => v.ProductQuantities) // ✅ Include đúng chỗ cần
	                    .FirstOrDefaultAsync(v => v.Id == cart.VariationId);


					if (variation == null)
                    {
                        TempData["error"] = $"Variation with ID {cart.VariationId} not found!";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }

                    if (cart.Quantity > variation.Stock)
                    {
                        TempData["error"] = $"Số lượng yêu cầu cho sản phẩm {variation.Product.Name} vượt quá tồn kho ({variation.Stock}).";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }

                    // Trừ tồn kho theo FIFO từ các lô
                    int quantityToDeduct = cart.Quantity;
                    var batchStocks = variation.ProductQuantities
                        .Where(pq => pq.CurrentQuantityInBatch > 0)
                        .OrderBy(pq => pq.DateCreated)
                        .ToList();

                    foreach (var pq in batchStocks)
                    {
                        if (quantityToDeduct <= 0) break;

                        int deduct = Math.Min(quantityToDeduct, pq.CurrentQuantityInBatch);
                        pq.CurrentQuantityInBatch -= deduct;
                        pq.LastUpdated = DateTime.Now;
                        quantityToDeduct -= deduct;

                        _datacontext.ProductQuantities.Update(pq);
                    }

                    // Tạo OrderDetails
                    decimal itemTotal = cart.Quantity * cart.Price;
                    decimal itemDiscount = itemTotal * discountRate;
                    decimal itemDiscountCoupon = (itemTotal / grandTotal) * discountAmountCoupon;

                    _datacontext.OrderDetails.Add(new OrderDetails
                    {
                        UserName = userEmail,
                        OrderCode = ordercode,
                        ProductId = variation.ProductId,
                        VariationId = variation.Id,
                        Price = cart.Price,
                        Quantity = cart.Quantity,
                        DiscountAmount = itemDiscount + itemDiscountCoupon
                    });

                    // Ghi nhận bảo hành nếu có
                    if (variation.Product.WarrantyPeriod > 0)
                    {
                        _datacontext.Warranties.Add(new WarrantyModel
                        {
                            WarrantyCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
                            ProductId = variation.ProductId,
                            VariationId = variation.Id,
                            OrderCode = ordercode,
                            ExpirationDate = DateTime.Now.AddYears(variation.Product.WarrantyPeriod),
                            CreatedDate = DateTime.Now
                        });
                    }

                    // Cập nhật số lượng đã bán
                    variation.Product.Sold += cart.Quantity;
                    _datacontext.Products.Update(variation.Product);
                }

                // Cập nhật coupon nếu có
                if (HttpContext.Session.GetString("CouponCode") is string couponCode && !string.IsNullOrEmpty(couponCode))
                {
                    var coupon = await _datacontext.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);
                    if (coupon != null)
                    {
                        coupon.UsedCount += 1;
                        _datacontext.Coupons.Update(coupon);
                    }
                }

                await _datacontext.SaveChangesAsync();

                // Gửi email xác nhận
                var orderDetails = await _datacontext.OrderDetails
                    .Where(o => o.OrderCode == ordercode)
                    .Include(o => o.Product)
                    .Include(o => o.Variation).ThenInclude(v => v.Material)
                    .Include(o => o.Variation).ThenInclude(v => v.Color)
                    .ToListAsync();

                var emailBody = new StringBuilder();
                var baseUrl = "http://localhost:5139/";

                emailBody.AppendLine("Dear Customer,");
                emailBody.AppendLine("We have successfully received your order. Here are the details:");
                emailBody.AppendLine();

                emailBody.AppendLine($"Order Code: {ordercode}");
                emailBody.AppendLine($"Order Date: {DateTime.Now:yyyy-MM-dd}");
                emailBody.AppendLine($"Shipping Cost: ${shippingPrice:F2}");
                emailBody.AppendLine($"Discount (membership): -${discountAmountMember:F2}");
                emailBody.AppendLine($"Discount (coupon): -${discountAmountCoupon:F2}");
                emailBody.AppendLine($"Final Total: ${finalTotal:F2}");
                emailBody.AppendLine($"Total Items: {cartItems.Count}");
                emailBody.AppendLine($"View your order: {baseUrl}Account/ViewOrder?orderCode={ordercode}");
                emailBody.AppendLine();
                emailBody.AppendLine("Products:");

                foreach (var item in orderDetails)
                {
                    var imageUrl = $"{baseUrl}media/variations/{item.Variation.ImageUrl}";
                    var material = item.Variation?.Material?.Name ?? "Unknown";
                    var color = item.Variation?.Color?.Name ?? "Unknown";
                    var size = item.Variation?.Size ?? 0;

                    emailBody.AppendLine($"- {item.Product.Name} ({material}, {color}, Size: {size})");
                    emailBody.AppendLine($"  Quantity: {item.Quantity}, Price: ${item.Price:F2}, Total: ${item.Quantity * item.Price:F2}");
                    emailBody.AppendLine($"  Image: {imageUrl}");
                    emailBody.AppendLine();
                }

                emailBody.AppendLine("Thank you for shopping with us!");
                await _emailSender.SendEmailAsync(userEmail, "Order Confirmation", emailBody.ToString());

                // Dọn session
                HttpContext.Session.Remove("Cart");
                HttpContext.Session.Remove("DiscountAmount");
                HttpContext.Session.Remove("CouponCode");
                HttpContext.Session.Remove("ShippingAddress");

                await transaction.CommitAsync();
                return View(order);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["error"] = $"Đã xảy ra lỗi: {ex.Message}";
                return RedirectToAction("Index", "Cart");
            }
        }



		[HttpGet, HttpPost]
		public async Task<IActionResult> OrderConfirmationStripe(string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                Console.WriteLine("DEBUG: session_id is null or empty.");
                return RedirectToAction("Index", "Cart");
            }
			var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            if (session == null || !session.Metadata.ContainsKey("ordercode"))
            {
                Console.WriteLine("DEBUG: Stripe session is invalid or missing ordercode.");
                return RedirectToAction("Index", "Cart");
            }

            string ordercode = session.Metadata["ordercode"];
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new();

            if (!cartItems.Any())
            {
                TempData["error"] = "No items in the cart to process!";
                return RedirectToAction("Index", "Cart");
            }

            await using var transaction = await _datacontext.Database.BeginTransactionAsync();

            try
            {
                var order = await _datacontext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
                string userEmail = order?.UserName ?? HttpContext.Session.GetString("UserEmail");

                var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                {
                    TempData["error"] = "User not found!";
                    await transaction.RollbackAsync();
                    return RedirectToAction("Index", "Cart");
                }

                decimal shippingPrice = 0;
                if (Request.Cookies["ShippingPrice"] is string shippingPriceCookie)
                    shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);

                decimal discountAmountCoupon = 0;
                if (HttpContext.Session.GetString("DiscountAmount") is string discountSession)
                    discountAmountCoupon = decimal.Parse(discountSession);

                decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
                decimal discountRate = user.GetDiscountRate();
                decimal discountAmountMember = grandTotal * discountRate;
                decimal finalTotal = grandTotal - discountAmountMember - discountAmountCoupon + shippingPrice;

                if (order != null)
                {
                    order.ShippingCost = shippingPrice;
                    order.Address = HttpContext.Session.GetString("ShippingAddress") ?? order.Address;
                    order.Status = 1;
                    order.PaymentIntentId = session.PaymentIntentId;
                    order.CreatedDate = DateTime.UtcNow;
                    _datacontext.Orders.Update(order);
                }
                else
                {
                    order = new OrderBuilder()
                        .SetOrderCode(ordercode)
                        .SetShippingCost(shippingPrice)
                        .SetAddress(HttpContext.Session.GetString("ShippingAddress"))
                        .SetUserName(userEmail)
                        .SetCreatedDate(DateTime.UtcNow)
                        .SetStatus(1)
                        .SetPaymentIntentId(session.PaymentIntentId)
                        .Build();

                    _datacontext.Orders.Add(order);
                }

                foreach (var cart in cartItems)
                {
                    var variation = await _datacontext.Variations
                        .Include(v => v.Material)
                        .Include(v => v.Color)
                        .Include(v => v.Product)
                            .ThenInclude(p => p.Variations)
                            .ThenInclude(v => v.ProductQuantities)
                        .FirstOrDefaultAsync(v => v.Id == cart.VariationId);

                    if (variation == null)
                    {
                        TempData["error"] = $"Variation with ID {cart.VariationId} not found!";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }

                    if (cart.Quantity > variation.Stock)
                    {
                        TempData["error"] = $"Số lượng yêu cầu cho sản phẩm {variation.Product.Name} vượt quá tồn kho ({variation.Stock}).";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }

                    // Trừ tồn kho từ các lô theo FIFO
                    int quantityToDeduct = cart.Quantity;
                    var productQuantities = variation.ProductQuantities
                        .Where(pq => pq.CurrentQuantityInBatch > 0)
                        .OrderBy(pq => pq.DateCreated)
                        .ToList();
					foreach (var pq in productQuantities)
                    {
                        if (quantityToDeduct <= 0) break;

                        int deduct = Math.Min(quantityToDeduct, pq.CurrentQuantityInBatch);
                        pq.CurrentQuantityInBatch -= deduct;
                        pq.LastUpdated = DateTime.UtcNow;
                        quantityToDeduct -= deduct;

						_datacontext.ProductQuantities.Update(pq);
                    }

                    // Thêm OrderDetail
                    decimal itemTotal = cart.Quantity * cart.Price;
                    decimal itemDiscount = itemTotal * discountRate;
                    decimal itemDiscountCoupon = (itemTotal / grandTotal) * discountAmountCoupon;

                    _datacontext.OrderDetails.Add(new OrderDetails
                    {
                        UserName = userEmail,
                        OrderCode = ordercode,
                        ProductId = variation.ProductId,
                        VariationId = variation.Id,
                        Price = cart.Price,
                        Quantity = cart.Quantity,
                        DiscountAmount = itemDiscount + itemDiscountCoupon
                    });

                    // Bảo hành nếu có
                    if (variation.Product.WarrantyPeriod > 0)
                    {
                        _datacontext.Warranties.Add(new WarrantyModel
                        {
                            WarrantyCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
                            ProductId = variation.ProductId,
                            VariationId = variation.Id,
                            OrderCode = ordercode,
                            ExpirationDate = DateTime.UtcNow.AddYears(variation.Product.WarrantyPeriod),
                            CreatedDate = DateTime.UtcNow
                        });
                    }

                    // Cập nhật số lượng đã bán (không cập nhật Product.Quantity nữa)
                    variation.Product.Sold += cart.Quantity;
                    _datacontext.Products.Update(variation.Product);
                }

                // Cập nhật phiếu giảm giá nếu có
                if (HttpContext.Session.GetString("CouponCode") is string couponCode && !string.IsNullOrEmpty(couponCode))
                {
                    var coupon = await _datacontext.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);
                    if (coupon != null)
                    {
                        coupon.UsedCount += 1;
                        _datacontext.Coupons.Update(coupon);
                    }
                }

                await _datacontext.SaveChangesAsync();

                // Gửi email
                var orderDetails = await _datacontext.OrderDetails
                    .Where(od => od.OrderCode == ordercode)
                    .Include(od => od.Product)
                    .Include(od => od.Variation).ThenInclude(v => v.Material)
                    .Include(od => od.Variation).ThenInclude(v => v.Color)
                    .ToListAsync();

                var baseUrl = "http://localhost:5139/";
                var emailBody = new StringBuilder();
                emailBody.AppendLine("Dear Customer,");
                emailBody.AppendLine("We have successfully received your order. Here are the details:");
                emailBody.AppendLine();

                emailBody.AppendLine($"Order Code: {ordercode}");
                emailBody.AppendLine($"Order Date: {DateTime.UtcNow:yyyy-MM-dd}");
                emailBody.AppendLine($"Shipping Cost: ${shippingPrice:F2}");
                emailBody.AppendLine($"Discount (membership): -${discountAmountMember:F2}");
                emailBody.AppendLine($"Discount (coupon): -${discountAmountCoupon:F2}");
                emailBody.AppendLine($"Final Total: ${finalTotal:F2}");
                emailBody.AppendLine($"Total Items: {cartItems.Count}");
                emailBody.AppendLine($"View your order: {baseUrl}Account/ViewOrder?orderCode={ordercode}");
                emailBody.AppendLine();

                emailBody.AppendLine("Products:");
                foreach (var item in orderDetails)
                {
                    var imageUrl = $"{baseUrl}media/variations/{item.Variation.ImageUrl}";
                    var material = item.Variation?.Material?.Name ?? "Unknown";
                    var color = item.Variation?.Color?.Name ?? "Unknown";
                    var size = item.Variation?.Size ?? 0;

                    emailBody.AppendLine($"- {item.Product.Name} ({material}, {color}, Size: {size})");
                    emailBody.AppendLine($"  Quantity: {item.Quantity}, Price: ${item.Price:F2}, Total: ${item.Quantity * item.Price:F2}");
                    emailBody.AppendLine($"  Image: {imageUrl}");
                    emailBody.AppendLine();
                }

                emailBody.AppendLine("Thank you for shopping with us!");
                await _emailSender.SendEmailAsync(userEmail, "Order Confirmation", emailBody.ToString());

                // Xóa session
                HttpContext.Session.Remove("Cart");
                HttpContext.Session.Remove("DiscountAmount");
                HttpContext.Session.Remove("CouponCode");
                HttpContext.Session.Remove("ShippingAddress");

                await transaction.CommitAsync();
                return View(order);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"ERROR: Failed to process order. {ex.Message}");
                TempData["error"] = $"Đã xảy ra lỗi khi xử lý đơn hàng: {ex.Message}";
                return RedirectToAction("Index", "Cart");
            }
        }

        //========
        //API
        //========
        [HttpPost]
        [Route("api/Checkout/Checkout")]
        public async Task<IActionResult> CheckoutApi([FromBody] CheckoutApiRequest request)
        {
            try
            {
                // Validate request
                if (request == null || !request.CartItems.Any())
                {
                    return BadRequest(new { error = "Invalid request or empty cart" });
                }

                // Tạo đơn hàng
                var order = new OrderModel
                {
                    OrderCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(),
                    CreatedDate = DateTime.Now,
                    UserName = request.UserEmail ?? "guest@example.com"
                };

                // Convert CartItems từ API request
                var cartItems = request.CartItems.Select(item => new CartItemModel
                {
                    ProductId = item.ProductId,
                    VariationId = item.VariationId,
                    ProductName = item.ProductName,
                    Price = (decimal)item.Price, // Ép kiểu double sang decimal
                    Quantity = item.Quantity,
                    Image = item.ImageUrl
                }).ToList();

                // Xử lý thanh toán - Ép kiểu double sang decimal
                var paymentService = PaymentServiceFactory.GetPaymentService(request.PaymentMethod, _datacontext);
                var redirectUrl = await paymentService.ProcessPayment(
                    order,
                    cartItems,
                    (decimal)request.ShippingPrice,    // Ép kiểu double sang decimal
                    (decimal)request.DiscountAmount    // Ép kiểu double sang decimal
                );

                return Ok(new
                {
                    success = true,
                    redirectUrl = redirectUrl,
                    orderCode = order.OrderCode,
                    paymentMethod = request.PaymentMethod
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public class CheckoutApiRequest
        {
            public string PaymentMethod { get; set; }
            public string UserEmail { get; set; }
            public List<CartItemApiModel> CartItems { get; set; }
            public double ShippingPrice { get; set; }
            public double DiscountAmount { get; set; }
        }

        public class CartItemApiModel
        {
            public int ProductId { get; set; }
            public int VariationId { get; set; }
            public string ProductName { get; set; }
            public double Price { get; set; }
            public int Quantity { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}

