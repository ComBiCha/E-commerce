using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_commerce.Controllers
{
	public class CartController : Controller
	{
		private readonly DataContext _dataContext;
		public CartController(DataContext dataContext)
		{
			_dataContext = dataContext;
		}

        public async Task<IActionResult> Index()
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			var shippingPriceCookie = Request.Cookies["ShippingPrice"];
			decimal shippingPrice = 0;

			if (shippingPriceCookie != null)
			{
				shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
			}

			// Lấy danh sách Variation từ database dựa trên VariationId
			var variationIds = cartItems.Select(x => x.VariationId).Distinct().ToList();
			var variations = _dataContext.Variations
			.Include(v => v.Material)
			.Include(v => v.Color)// Load thêm Material vào Variation
			.Where(v => variationIds.Contains(v.Id))
			.ToDictionary(v => v.Id);

			string discountStr = HttpContext.Session.GetString("DiscountAmount");
			decimal discountAmount2 = string.IsNullOrEmpty(discountStr) ? 0 : Convert.ToDecimal(discountStr);


			decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
			decimal discountRate = user?.GetDiscountRate() ?? 0m;
			decimal discountAmount = grandTotal * discountRate;
			decimal finalTotal = grandTotal - discountAmount - discountAmount2 + shippingPrice;

			// Gán thông tin Variation vào từng CartItemModel
			foreach (var item in cartItems)
			{
				if (variations.ContainsKey(item.VariationId))
				{
					item.Variation = variations[item.VariationId];
				}
			}

			string couponCode = HttpContext.Session.GetString("CouponCode") ?? "";

			CartItemViewModel cartVM = new()
			{
				CartItems = cartItems,
				GrandTotal = grandTotal,
                ShippingCost = shippingPrice,
                DiscountAmount = discountAmount,
				DiscountAmount2 = discountAmount2,
				CouponCode = couponCode,
                FinalTotal = finalTotal
            };

			return View(cartVM);
		}

		public IActionResult Checkout()
		{
			return View("~/Views/Checkout/Index.cshtml");
		}
		[HttpPost]
		public async Task<IActionResult> Add(long productId, int? variationId)
		{
			ProductModel product = await _dataContext.Products.FindAsync(productId);
			if (product == null) return NotFound();

			// Lấy danh sách cart từ session
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

			// Kiểm tra xem variation có tồn tại không
			ProductVariationModel variation = null;
			if (variationId.HasValue)
			{
				variation = await _dataContext.Variations.FindAsync(variationId.Value);
				if (variation == null) return NotFound();
			}

			// Tìm item trong giỏ hàng
			CartItemModel cartItem = cart.FirstOrDefault(c => c.ProductId == productId && c.VariationId == variationId);

			if (cartItem == null)
			{
				cart.Add(new CartItemModel
				{
					ProductId = product.Id,
					ProductName = product.Name,
					Price = variation != null ? variation.Price : product.Price,
					Quantity = 1,
					Image = variation != null ? variation.ImageUrl : product.Image,
					VariationId = variation.Id
				});
			}
			else
			{
				cartItem.Quantity += 1;
			}

			// Cập nhật session
			HttpContext.Session.SetJson("Cart", cart);

			return Json(new { success = true });
		}

		public async Task<IActionResult> Decrease(long Id)
		{
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");

			CartItemModel cartitem = cart.Where(c => c.ProductId == Id).FirstOrDefault();

			if (cartitem.Quantity > 1)
			{
				--cartitem.Quantity;
			}
			else
			{
				cart.RemoveAll(p => p.ProductId == Id);
			}
			if (cart.Count == 0)
			{
				HttpContext.Session.Remove("Cart");
			}
			else
			{
				HttpContext.Session.SetJson("Cart", cart);
			}

			return RedirectToAction("Index");
		}
		public async Task<IActionResult> Increase(long Id)
		{
			// Lấy Cart từ Session
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");

			// Tìm CartItem tương ứng
			CartItemModel cartitem = cart.FirstOrDefault(c => c.ProductId == Id);
			if (cartitem == null)
			{
				return RedirectToAction("Index"); // Không tìm thấy item trong giỏ hàng
			}

			// Lấy thông tin Variation từ DB (bao gồm số lượng tồn kho)
			var variation = await _dataContext.Variations
				.Where(v => v.Id == cartitem.VariationId)
				.FirstOrDefaultAsync();

			if (variation == null)
			{
				TempData["error"] = "This product variation does not exist.";
				return RedirectToAction("Index");
			}

			// Kiểm tra số lượng tồn kho
			if (cartitem.Quantity < variation.Stock)
			{
				cartitem.Quantity++;
			}
			else
			{
				cartitem.Quantity = variation.Stock;
				TempData["error"] = $"The Maximum Quantity available for this variation is {variation.Stock}";
			}

			// Cập nhật lại session
			if (cart.Count == 0)
			{
				HttpContext.Session.Remove("Cart");
			}
			else
			{
				HttpContext.Session.SetJson("Cart", cart);
			}

			return RedirectToAction("Index");
		}

		public async Task<IActionResult> Remove(long Id)
		{
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");

			cart.RemoveAll(p => p.ProductId == Id);
			if (cart.Count == 0)
			{
				HttpContext.Session.Remove("Cart");
			}
			else
			{
				HttpContext.Session.SetJson("Cart", cart);
			}
            TempData["success"] = "Remove Item Successfully";
            return RedirectToAction("Index");
		}
		public async Task<IActionResult> Clear(long Id)
		{
			HttpContext.Session.Remove("Cart");
            TempData["success"] = "Clear cart Successfully";
            return RedirectToAction("Index");
		}
		[HttpPost]
		public async Task<IActionResult> GetShippingPrice(ShippingModel shippingModel, string quan, string tinh, string phuong, string detailAddress)
		{
			var existingShipping = await _dataContext.Shippings.FirstOrDefaultAsync(x => x.City == tinh && x.District == quan && x.Ward == phuong);

            HttpContext.Session.SetString("ShippingAddress", $"{detailAddress}, {phuong}, {quan}, {tinh}");

            decimal shippingPrice = 0;

			if(existingShipping != null)
			{
				shippingPrice = existingShipping.Price;
			}
			else
			{
				shippingPrice = 5;
			}
			var shippingPriceJson = JsonConvert.SerializeObject(shippingPrice);
			try
			{
				var cookieOptions = new CookieOptions
				{
					HttpOnly = true,
					Expires = DateTimeOffset.UtcNow.AddMinutes(30),
					Secure = true
				};

				Response.Cookies.Append("ShippingPrice", shippingPriceJson, cookieOptions);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error adding shipping price cookie: {ex.Message}");
			}
			return Json(new { shippingPrice });
		}
		[HttpGet]
		public IActionResult DeleteShippingPrice()
		{
			Response.Cookies.Delete("ShippingPrice");
			return RedirectToAction("Index","Cart");
		}

		[HttpPost]
		public async Task<IActionResult> ApplyCoupon(string couponCode)
		{
			var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);

			if (coupon == null || coupon.ExpiryDate < DateTime.Now || coupon.UsedCount >= coupon.MaxUsage)
			{
				TempData["error"] = "Invalid or expired coupon code!";
				return RedirectToAction("Index");

			}

			// Lấy giỏ hàng từ session
			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);

			if (coupon.MinOrderValue.HasValue && grandTotal < coupon.MinOrderValue.Value)
			{
				TempData["error"] = $"Minimum order value must be ${coupon.MinOrderValue.Value} to apply this coupon!";
				return RedirectToAction("Index");

			}

			decimal discount = coupon.IsPercentage ? (grandTotal * coupon.DiscountAmount / 100) : coupon.DiscountAmount;
			HttpContext.Session.SetString("DiscountAmount", discount.ToString());
			HttpContext.Session.SetString("CouponCode", couponCode);

			TempData["success"] = $"Coupon applied successfully! Discount: ${discount:F2}";
			return RedirectToAction("Index");

		}

		public IActionResult RemoveCoupon()
{
    HttpContext.Session.Remove("DiscountAmount");
    TempData["success"] = "Coupon removed successfully!";
    return RedirectToAction("Index");
}


	}
}
