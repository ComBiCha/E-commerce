using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace E_commerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly DataContext _datacontext;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<AppUserModel> _userManager;

        public HomeController(ILogger<HomeController> logger, DataContext context, UserManager<AppUserModel> userManager)
        {
            _logger = logger;
            _datacontext = context;
			_userManager = userManager;

		}

        public IActionResult Index()
        {
            var products = _datacontext.Products.Include("Category").Include("Brand").ToList();
            var sliders = _datacontext.Sliders.Where(s => s.Status == 1).ToList();

            // L?y danh sách các brand cùng v?i s? l??ng s?n ph?m t??ng ?ng
            var brandCounts = _datacontext.Brands
                .Select(b => new
                {
                    b.Name,
                    b.Slug,
                    ProductCount = _datacontext.Products.Count(p => p.BrandId == b.Id)
                })
                .ToList();
            var contact = _datacontext.Contacts.FirstOrDefault();
            ViewBag.BrandCounts = brandCounts;
            ViewBag.Sliders = sliders;
            ViewBag.Contact = contact;

            return View(products);
        }


        public IActionResult Privacy()
        {
            return View();
        }
		public async Task<IActionResult> Contact()
		{
            var contact = await _datacontext.Contacts.FirstAsync();
			return View(contact);
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int statuscode)
        {
            if (statuscode == 404)
            {
                return View("NotFound");
            }
            else
            {
                return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }
        /*public async Task<IActionResult> AddWishList(long Id, WishlistModel wishlistmodel)
        {
            var user = await _userManager.GetUserAsync(User);

            var wishlistProduct = new WishlistModel
            {
                ProductId = Id,
                UserId = user.Id
            };

            _datacontext.Wishlists.Add(wishlistProduct);

            try
            {
                await _datacontext.SaveChangesAsync();
                return Ok(new { success = true, message = "Add to wishlist successfully!" });
            }
            catch(Exception)
            {
                return StatusCode(500, "An error occurred while adding to wishlist.");
            }
        }*/
        public async Task<IActionResult> AddWishList(long Id)
        {
            // Get the current user (assume you have a way to retrieve the logged-in user's Id)
            var userId = await _userManager.GetUserAsync(User);  // Modify based on how you get UserId

            // Check if the product is already in the wishlist for this user
            var existingWishlist = await _datacontext.Wishlists
                .FirstOrDefaultAsync(w => w.ProductId == Id && w.UserId == userId.Id);

            if (existingWishlist != null)
            {
                TempData["error"] = "Product is already in your wishlist.";
                return RedirectToAction("Wishlist", "Home");
            }

            // If not, add the product to the wishlist
            var newWishlist = new WishlistModel
            {
                ProductId = Id,
                UserId = userId.Id
            };

            _datacontext.Wishlists.Add(newWishlist);
            await _datacontext.SaveChangesAsync();
            TempData["success"] = "Product added to wishlist successfully.";

            return RedirectToAction("Wishlist", "Home");
        }

        /*public async Task<IActionResult> AddCompare(long Id)
		{
			var user = await _userManager.GetUserAsync(User);

			var compareProduct = new CompareModel
			{
				ProductId = Id,
				UserId = user.Id
			};

			_datacontext.Compares.Add(compareProduct);

			try
			{
				await _datacontext.SaveChangesAsync();
				return Ok(new { success = true, message = "Add to compare successfully!" });
			}
			catch (Exception)
			{
				return StatusCode(500, "An error occurred while adding to wishlist.");
			}
		}*/
        public async Task<IActionResult> AddCompare(long Id)
        {
            // Get the current user (assume you have a way to retrieve the logged-in user's Id)
            var userId = await _userManager.GetUserAsync(User);

            // Check if the product is already in the compare list for this user
            var existingCompare = await _datacontext.Compares
                .FirstOrDefaultAsync(c => c.ProductId == Id && c.UserId == userId.Id);

            if (existingCompare != null)
            {
                TempData["error"] = "Product is already in your compare list.";
                return RedirectToAction("Compare", "Home");
            }

            // If not, add the product to the compare list
            var newCompare = new CompareModel
            {
                ProductId = Id,
                UserId = userId.Id
            };

            _datacontext.Compares.Add(newCompare);
            await _datacontext.SaveChangesAsync();
            TempData["success"] = "Product added to compare successfully.";

            return RedirectToAction("Compare", "Home");
        }

        public async Task<IActionResult> Compare()
        {
            var compare_product = await (from c in _datacontext.Compares
                                         join p in _datacontext.Products on c.ProductId equals p.Id
                                         join u in _datacontext.Users on c.UserId equals u.Id
                                         select new { User = u, Product = p, Compares = c }).ToListAsync();

            return View(compare_product);
        }
		public async Task<IActionResult> Wishlist()
		{
			var wishlist_product = await (from w in _datacontext.Wishlists
										 join p in _datacontext.Products on w.ProductId equals p.Id
										 join u in _datacontext.Users on w.UserId equals u.Id
										 select new { User = u, Product = p, Wishlists = w }).ToListAsync();

			return View(wishlist_product);
		}
        public async Task<IActionResult> DeleteCompare(int Id)
        {
            CompareModel compare = await _datacontext.Compares.FindAsync(Id);
            _datacontext.Compares.Remove(compare);
            await _datacontext.SaveChangesAsync();
            TempData["success"] = "Compare removed successfully";
            return RedirectToAction("Compare","Home");
        }
        public async Task<IActionResult> DeleteWishlist(int Id)
        {
            WishlistModel wishlist = await _datacontext.Wishlists.FindAsync(Id);
            _datacontext.Wishlists.Remove(wishlist);
            await _datacontext.SaveChangesAsync();
            TempData["success"] = "Wishlist removed successfully";
            return RedirectToAction("Wishlist","Home");
        }
        public async Task<IActionResult> Account()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            var userModel = new UserModel { UserName = user.UserName, Email = user.Email };
            return View(userModel);
        }
    }
}
