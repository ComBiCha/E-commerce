using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
	[Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MaterialController : Controller
	{
		private readonly DataContext _dataContext;
		public MaterialController(DataContext context)
		{
			_dataContext = context;
		}
        public async Task<IActionResult> Index(int pg = 1)
        {
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            // Đếm tổng số records
            int recsCount = await _dataContext.Materials.CountAsync();
            var pager = new Paginate(recsCount, pg, pageSize);

            // Lấy data với Skip/Take trực tiếp từ DB
            int recSkip = (pg - 1) * pageSize;
            var materials = await _dataContext.Materials
                .OrderBy(p => p.Id) // Sắp xếp tăng dần (ai tạo trước ở trên)
                .Skip(recSkip)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Pager = pager;
            return View(materials);
        }
        /*public async Task<IActionResult> Index(int pg = 1)
        {
            List<CategoryModel> category = _dataContext.Categories.ToList(); //33 datas

            const int pageSize = 10; //10 items/trang

            if (pg < 1) //page < 1;
            {
                pg = 1; //page ==1
            }
            int recsCount = category.Count(); //33 items;

            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize; //(3 - 1) * 10; 

            //category.Skip(20).Take(10).ToList()

            var data = category.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }*/
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaterialModel material)
        {

            if (ModelState.IsValid)
            {
                var name = await _dataContext.Materials.FirstOrDefaultAsync(p => p.Name == material.Name);
                if (name != null)
                {
                    ModelState.AddModelError("", "Category already exist");
                    return View(material);
                }
                _dataContext.Add(material);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Category added successfully";
                return RedirectToAction("Index");
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
                return BadRequest(errorMessage);
            }
        }
        public async Task<IActionResult> Delete(int Id)
        {
            MaterialModel material = await _dataContext.Materials.FindAsync(Id);
            _dataContext.Materials.Remove(material);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Product removed successfully";
            return RedirectToAction("Index");
        }
        public async Task<IActionResult> Edit(int Id)
        {
            MaterialModel material = await _dataContext.Materials.FindAsync(Id);
            return View(material);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MaterialModel material, int Id)
        {
            if (ModelState.IsValid)
            {
                var name = await _dataContext.Categories.FirstOrDefaultAsync(p => p.Name == material.Name);
                if (name != null)
                {
                    ModelState.AddModelError("", "Category already exist");
                    return View(material);
                }
                _dataContext.Update(material);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Category updated successfully";
                return RedirectToAction("Index");
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
                return BadRequest(errorMessage);
            }
        }
    }
}
