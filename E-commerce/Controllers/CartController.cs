using System.Security.Claims;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_commerce.Controllers
{
	public class CartController : Controller
	{
		private readonly DataContext _dataContext;
		private readonly CouponManager _couponManager;
		public CartController(DataContext dataContext, CouponManager couponManager)
		{
			_dataContext = dataContext;
			_couponManager = couponManager;
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
			.Include(p => p.ProductQuantities)
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
					HttpOnly = false,
					Expires = DateTimeOffset.UtcNow.AddMinutes(30),
					Secure = false,
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
			// Lấy giỏ hàng từ session
			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);

			// Lấy userId hiện tại
			var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

			// Kiểm tra user còn voucher này không
			var userVoucher = await _dataContext.UserVouchers
				.Where(v => v.UserId == userId && v.CouponCode == couponCode && !v.IsUsed)
				.OrderBy(v => v.ReceivedAt)
				.FirstOrDefaultAsync();

			if (userVoucher == null)
			{
				TempData["error"] = "You do not own this voucher or it has already been used!";
				return RedirectToAction("Index");
			}

			// Áp dụng coupon
			var (success, message, discount) = await _couponManager.ApplyCouponAsync(couponCode, grandTotal);
			if (success)
			{
				// Đánh dấu voucher đã dùng (hoặc xóa nếu muốn)
				_dataContext.UserVouchers.Remove(userVoucher); // hoặc userVoucher.IsUsed = true;
				await _dataContext.SaveChangesAsync();

				HttpContext.Session.SetString("DiscountAmount", discount.ToString());
				HttpContext.Session.SetString("CouponCode", couponCode);
				TempData["success"] = message;
			}
			else
			{
				TempData["error"] = message;
			}

			return RedirectToAction("Index");
		}

        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove("DiscountAmount");
            HttpContext.Session.Remove("CouponCode");
            TempData["success"] = "Coupon removed successfully!";
            return RedirectToAction("Index");
        }


/*        public IActionResult RemoveCoupon()
{
    HttpContext.Session.Remove("DiscountAmount");
    TempData["success"] = "Coupon removed successfully!";
    return RedirectToAction("Index");
}*/


        public async Task<IActionResult> OrderSummaryPartial()
        {
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
            string discountStr = HttpContext.Session.GetString("DiscountAmount");
            decimal discountAmount2 = string.IsNullOrEmpty(discountStr) ? 0 : Convert.ToDecimal(discountStr);

            string couponCode = HttpContext.Session.GetString("CouponCode") ?? "";

            decimal discountRate = user?.GetDiscountRate() ?? 0m;
            decimal discountAmount = grandTotal * discountRate;
            var shippingPriceCookie = Request.Cookies["ShippingPrice"];
            decimal shippingPrice = string.IsNullOrEmpty(shippingPriceCookie) ? 0 : JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
            decimal finalTotal = grandTotal - discountAmount - discountAmount2 + shippingPrice;

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

            return PartialView("/Views/Shared/Components/Carts/_OrderSummaryPartial.cshtml", cartVM);
        }


		[HttpPost]
		public async Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityModel model)
		{
			if (model == null)
			{
				return Json(new { success = false, error = "Invalid data received!" });
			}

			Console.WriteLine($"Request: ProductId = {model.ProductId}, VariationId = {model.VariationId}, Quantity = {model.NewQuantity}");

			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

			// Tìm sản phẩm trong giỏ
			var cartItem = cart.FirstOrDefault(c =>
				c.ProductId == model.ProductId &&
				c.VariationId == model.VariationId);

			if (cartItem == null)
			{
				return Json(new { success = false, error = "Product not found in the cart!" });
			}

			// Lấy biến thể từ DB kèm tồn kho
			var variation = await _dataContext.Variations
				.Include(v => v.ProductQuantities)
				.FirstOrDefaultAsync(v => v.Id == model.VariationId);

			if (variation == null)
			{
				return Json(new { success = false, error = "This product variation does not exist!" });
			}

			// Tính tổng tồn kho
			int availableStock = variation.ProductQuantities?.Sum(q => q.CurrentQuantityInBatch) ?? 0;

			if (model.NewQuantity > availableStock)
			{
				return Json(new
				{
					success = false,
					error = $"The maximum quantity available for this product is {availableStock}."
				});
			}

			// Cập nhật giỏ hàng
			cartItem.Quantity = model.NewQuantity;
			HttpContext.Session.SetJson("Cart", cart);

			decimal newPrice = cartItem.Quantity * cartItem.Price;

			return Json(new
			{
				success = true,
				newPrice = newPrice
			});
		}

		//==================================================================================================
		//============================================API SESSION===========================================
		//==================================================================================================
		[HttpGet("api/Cart/GetCart")]
        public async Task<IActionResult> GetCart()
        {
            // Lấy cart từ session
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            // Lấy danh sách Variation từ database dựa trên VariationId
            var variationIds = cartItems.Select(x => x.VariationId).Distinct().ToList();
            var variations = await _dataContext.Variations
                .Include(v => v.Material)
                .Include(v => v.Color)
                .Where(v => variationIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            // Gán thông tin Variation vào từng CartItemModel
            foreach (var item in cartItems)
            {
                if (variations.ContainsKey(item.VariationId))
                {
                    item.Variation = variations[item.VariationId];
                }
            }

            return Json(cartItems);
        }



    }
}
