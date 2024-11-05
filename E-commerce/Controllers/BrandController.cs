using E_commerce.Migrations;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Controllers
{
	public class BrandController : Controller
	{
		private readonly DataContext _dataContext;
		public BrandController(DataContext context)
		{
			_dataContext = context;
		}
		public async Task<IActionResult> Index(string Slug = "")
		{
			BrandModel brand = _dataContext.Brands.Where(b => b.Slug == Slug).FirstOrDefault();

			if (brand == null) return RedirectToAction("Index");

			var productsByBrand = _dataContext.Products.Where(p => p.BrandId == brand.Id);

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

			return View(await productsByBrand.OrderByDescending(p => p.Id).ToListAsync());
		}
	}
}
