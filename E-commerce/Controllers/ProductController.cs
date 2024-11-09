using E_commerce.Migrations;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Controllers
{
	public class ProductController : Controller
	{
		private readonly DataContext _dataContext;
		public ProductController(DataContext context)
		{
			_dataContext = context;
		}
		public IActionResult Index()
		{
			return View();
		}
		public async Task<IActionResult> Search(string searchTerm)
		{
            ViewBag.Keyword = searchTerm;

            var brandCounts = _dataContext.Brands
                .Select(b => new
                {
                    b.Name,
                    b.Slug,
                    ProductCount = _dataContext.Products.Count(p => p.BrandId == b.Id)
                })
                .ToList();
            var contact = _dataContext.Contacts.FirstOrDefault();
            ViewBag.BrandCounts = brandCounts;
            ViewBag.Contact = contact;
            if (searchTerm == null)
			{
                var products = await _dataContext.Products.Include("Category").Include("Brand").ToListAsync();
                return View(products);
            }
            else
            {
                var products = await _dataContext.Products.Where(p => p.Name.Contains(searchTerm) || p.Category.Name.Contains(searchTerm)).ToListAsync();
                return View(products);
            }

		}
		public async Task<IActionResult> Details(long Id)
		{
			if (Id == null) return RedirectToAction("Index");

			var productsById = _dataContext.Products.Include(p => p.Ratings).Where(p => p.Id == Id).FirstOrDefault();
			//related
			var relatedProducts = await _dataContext.Products.Where(p => p.CategoryId == productsById.CategoryId && p.Id != productsById.Id)
				.Take(4)
				.ToListAsync();
			ViewBag.RelatedProducts = relatedProducts;

            var groupedRelatedProducts = relatedProducts
			.Select((value, index) => new GroupedProduct { Index = index, Product = value })
			.GroupBy(x => x.Index / 3)
			.ToList();

            var brandCounts = _dataContext.Brands
				.Select(b => new
				{
					b.Name,
					b.Slug,
					ProductCount = _dataContext.Products.Count(p => p.BrandId == b.Id)
				})
				.ToList();
			var contact = _dataContext.Contacts.FirstOrDefault();
			ViewBag.BrandCounts = brandCounts;
			ViewBag.Contact = contact;

			var viewModel = new ProductDetailsViewModel
			{
				ProductDetails = productsById,
                RelatedProductsGrouped = groupedRelatedProducts,
            };

			return View(viewModel);
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CommentProduct(RatingModel rating)
		{
			if(ModelState.IsValid)
			{
				var ratingEntity = new RatingModel
				{
					ProductId = rating.ProductId,
					Name = rating.Name,
					Email = rating.Email,
					Comment = rating.Comment,
					Star = rating.Star
				};
				_dataContext.Ratings.Add(ratingEntity);
				await _dataContext.SaveChangesAsync();

				TempData["success"] = "Review success!";

				return Redirect(Request.Headers["Referer"]);

			}
			else
			{
				TempData["error"] = "Model error";
				List<string> errors = new List<string>();
				foreach (var value in ModelState.Values)
				{
					foreach (var error in value.Errors)
					{
						errors.Add(error.ErrorMessage);
					}
				}
				string errorMessage = string.Join("\n", errors);
				return RedirectToAction("Detail", new { id = rating.ProductId });
			}
			
		}
	}
}
