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

			// Lấy giỏ hàng từ session
			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			if (!cartItems.Any())
			{
				TempData["error"] = "Your cart is empty!";
				return RedirectToAction("Cart", "Cart");
			}

			var domain = "http://localhost:5139/";
			var options = new Stripe.Checkout.SessionCreateOptions
			{
				SuccessUrl = domain + $"Checkout/OrderConfirmation?ordercode={Guid.NewGuid()}",
				CancelUrl = domain + "Cart",
				LineItems = new List<SessionLineItemOptions>(),
				Mode = "payment"
			};

			foreach (var cart in cartItems)
			{
				var variation = await _datacontext.Variations
					.Include(v => v.Material)
					.Include(v => v.Color)
					.FirstOrDefaultAsync(v => v.Id == cart.VariationId);


				if (variation == null)
				{
					TempData["error"] = "One or more items in your cart are no longer available.";
					return RedirectToAction("Cart", "Cart");
				}

				var materialName = variation.Material?.Name ?? "Unknown Material";
				var colorName = variation.Color?.Name ?? "Unknown Color";

				var sessionListItem = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(cart.Price * 100),
						Currency = "usd",
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = $"{cart.ProductName} ({materialName} - {colorName})"
						}
					},
					Quantity = cart.Quantity
				};


				options.LineItems.Add(sessionListItem);
			}

			// Thêm phí vận chuyển nếu có
			var shippingPriceCookie = Request.Cookies["ShippingPrice"];
			if (shippingPriceCookie != null)
			{
				var shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
				var shippingLineItem = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(shippingPrice * 100),
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
			return Redirect(session.Url);
		}

		public async Task<IActionResult> OrderConfirmation(string ordercode)
		{
			var userEmail = User.FindFirstValue(ClaimTypes.Email);
			if (userEmail == null)
			{
				return RedirectToAction("Login", "Account");
			}

			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			if (!cartItems.Any())
			{
				TempData["error"] = "No items in the cart to process!";
				return RedirectToAction("Cart", "Cart");
			}

			var shippingPriceCookie = Request.Cookies["ShippingPrice"];
			decimal shippingPrice = shippingPriceCookie != null ? JsonConvert.DeserializeObject<decimal>(shippingPriceCookie) : 0;

			var shippingAddress = HttpContext.Session.GetString("ShippingAddress");

			// Tạo đơn hàng mới
			var order = new OrderModel
			{
				OrderCode = ordercode,
				ShippingCost = shippingPrice,
				Address = shippingAddress,
				UserName = userEmail,
				Status = 1,
				CreatedDate = DateTime.Now
			};
			_datacontext.Add(order);
			await _datacontext.SaveChangesAsync();

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

				// Thêm vào OrderDetails
				var orderDetail = new OrderDetails
				{
					UserName = userEmail,
					OrderCode = ordercode,
					ProductId = variation.ProductId,
					VariationId = variation.Id,
					Price = cart.Price,
					Quantity = cart.Quantity
				};
				_datacontext.Add(orderDetail);

				// Tạo mã bảo hành nếu sản phẩm có thời hạn bảo hành
				if (variation.Product.WarrantyPeriod > 0)
				{
					var warrantyCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(); // Mã bảo hành ngẫu nhiên
					var warranty = new WarrantyModel
					{
						WarrantyCode = warrantyCode,
						ProductId = variation.ProductId,
						VariationId = variation.Id,
						OrderCode = ordercode,
						ExpirationDate = DateTime.Now.AddMonths(variation.Product.WarrantyPeriod), // Lấy thời hạn bảo hành từ sản phẩm
						CreatedDate = DateTime.Now
					};
					_datacontext.Add(warranty);
				}


				// Cập nhật kho hàng
				variation.Stock -= cart.Quantity;
				variation.Product.Sold += cart.Quantity;
				_datacontext.Update(variation);
			}
			await _datacontext.SaveChangesAsync();

			// Gửi email xác nhận đơn hàng
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
			emailBody.AppendLine($"**Total Items:** {cartItems.Count}");
			emailBody.AppendLine();

			var baseUrl = "http://localhost:5139/";
			emailBody.AppendLine("**Products in Your Order:**");
			foreach (var detail in orderDetails)
			{
				var productImageUrl = $"{baseUrl}/media/products/{detail.Product.Image}";
				var materialName = detail.Variation?.Material?.Name ?? "Unknown Material";
				var colorName = detail.Variation?.Color?.Name ?? "Unknown Color";
				emailBody.AppendLine($"- **Product Name**: {detail.Product.Name} ({materialName} - {colorName})");
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

			TempData["success"] = "Checkout successfully";
			HttpContext.Session.Remove("Cart");
			return RedirectToAction("Index", "Cart");
		}
	}
}
