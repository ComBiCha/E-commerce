using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Controllers
{
	public class CartController : Controller
	{
		private readonly DataContext _dataContext;
		public CartController(DataContext dataContext)
		{
			_dataContext = dataContext;
		}

		public IActionResult Index()
		{
			List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			CartItemViewModel cartVM = new()
			{
				CartItems = cartItems,
				GrandTotal = cartItems.Sum(x => x.Quantity * x.Price)
			};
			return View(cartVM);
		}
		public IActionResult Checkout()
		{
			return View("~/Views/Checkout/Index.cshtml");
		}
		public async Task<IActionResult> Add(long Id)
		{
			ProductModel product = await _dataContext.Products.FindAsync(Id);
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
			CartItemModel cartItems = cart.Where(c => c.ProductId == Id).FirstOrDefault();

			if (cartItems == null)
			{
				cart.Add(new CartItemModel(product));
			}
			else
			{
				cartItems.Quantity += 1;
			}

			HttpContext.Session.SetJson("Cart", cart);

			TempData["success"] = "Add Item to cart Successfully";
			return Redirect(Request.Headers["Referer"].ToString());
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
			List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");

			CartItemModel cartitem = cart.Where(c => c.ProductId == Id).FirstOrDefault();

			if (cartitem.Quantity >= 1)
			{
				++cartitem.Quantity;
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
	}
}
