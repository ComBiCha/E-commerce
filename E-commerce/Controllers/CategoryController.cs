using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Controllers
{
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;
        public CategoryController(DataContext context)
        {
            _dataContext = context;
        }
        public async Task<IActionResult> Index(string Slug="")
        {
            CategoryModel category = _dataContext.Categories.Where(c => c.Slug == Slug).FirstOrDefault();

            if (category == null) return RedirectToAction("Index");

            var productsByCategory = _dataContext.Products.Where(p => p.CategoryId == category.Id);

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

			return View(await productsByCategory.OrderByDescending(p => p.Id).ToListAsync());
        }
    }
}
