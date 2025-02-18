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
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 9;  // S? l??ng s?n ph?m trên m?i trang
            int totalProducts = await _dataContext.Products.CountAsync();  // T?ng s? s?n ph?m
            var products = await _dataContext.Products
                .Include("Category")
                .Include("Brand")
                .Skip((page - 1) * pageSize)  // B? qua các s?n ph?m c?a các trang tr??c
                .Take(pageSize)  // L?y s? l??ng s?n ph?m c?a trang hi?n t?i
                .ToListAsync();

            var sliders = _dataContext.Sliders.Where(s => s.Status == 1).ToList();

            // L?y danh sách các brand cùng v?i s? l??ng s?n ph?m t??ng ?ng
            var brandCounts = _dataContext.Brands
                .Select(b => new
                {
                    b.Name,
                    b.Slug,
                    ProductCount = _dataContext.Products.Count(p => p.BrandId == b.Id)
                })
                .ToList();

            var contact = _dataContext.Contacts.FirstOrDefault();

            // Thêm thông tin phân trang vào ViewBag
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
            ViewBag.BrandCounts = brandCounts;
            ViewBag.Sliders = sliders;
            ViewBag.Contact = contact;

            return View(products);
        }
        public async Task<IActionResult> Search(string searchTerm, string category, string brand)
        {
            ViewBag.Keyword = searchTerm ?? $"{category} {brand}";

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

            IQueryable<ProductModel> products = _dataContext.Products.Include(p => p.Category).Include(p => p.Brand);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p => p.Name.Contains(searchTerm) || p.Category.Name.Contains(searchTerm));
            }
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category.Slug == category);
            }
            if (!string.IsNullOrEmpty(brand))
            {
                products = products.Where(p => p.Brand.Name == brand);
            }

            return View(await products.ToListAsync());
        }

        public async Task<IActionResult> Details(long Id)
        {
            if (Id == null) return RedirectToAction("Index");

			var productsById = _dataContext.Products
				.Include(p => p.Ratings)
				.Include(p => p.Variations)
					.ThenInclude(v => v.Material) // Load Material của Variations
				.Include(p => p.Variations)
					.ThenInclude(v => v.Color) // Load Color của Variations
				.FirstOrDefault(p => p.Id == Id);


			if (productsById == null) return NotFound();

            // Lấy danh sách sản phẩm liên quan
            var relatedProducts = await _dataContext.Products
                .Where(p => p.CategoryId == productsById.CategoryId && p.Id != productsById.Id)
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
                Variations = productsById.Variations.ToList()
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
