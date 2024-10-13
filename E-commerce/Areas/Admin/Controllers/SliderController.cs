using E_commerce.Migrations;
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
	public class SliderController : Controller
	{
		private readonly DataContext _dataContext;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public SliderController(DataContext context, IWebHostEnvironment webHostEnvironment)
		{
			_dataContext = context;
            _webHostEnvironment = webHostEnvironment;
        }
		public async Task<IActionResult> Index()
		{
			return View(await _dataContext.Sliders.OrderByDescending(p => p.Id).ToListAsync());
		}
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SliderModel slider)
        {
            if (ModelState.IsValid)
            {
                    if (slider.ImageUpload != null)
                    {
                        string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/sliders");
                        string imageName = Guid.NewGuid().ToString() + "_" + slider.ImageUpload.FileName;
                        string filePath = Path.Combine(uploadDir, imageName);

                        FileStream fs = new FileStream(filePath, FileMode.Create);
                        await slider.ImageUpload.CopyToAsync(fs);
                        fs.Close();
                        slider.Image = imageName;
                    }
                _dataContext.Add(slider);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Slider added successfully";
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
        public async Task<IActionResult> Edit(int Id)
        {
            SliderModel slider = await _dataContext.Sliders.FindAsync(Id);
            ViewBag.OldImage = slider.Image;

            return View(slider);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SliderModel slider)
        {
            var existed_slider = _dataContext.Sliders.Find(slider.Id);

            if (ModelState.IsValid)
            {

                if (slider.ImageUpload != null)
                {


                    string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/sliders");
                    string imageName = Guid.NewGuid().ToString() + "_" + slider.ImageUpload.FileName;
                    string filePath = Path.Combine(uploadDir, imageName);

                    string oldfilePath = Path.Combine(uploadDir, existed_slider.Image);
                    try
                    {
                        if (System.IO.File.Exists(oldfilePath))
                        {
                            System.IO.File.Delete(oldfilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "An error occurred while deleting the product image.");
                    }

                    FileStream fs = new FileStream(filePath, FileMode.Create);
                    await slider.ImageUpload.CopyToAsync(fs);
                    fs.Close();
                    existed_slider.Image = imageName;


                }
                existed_slider.Name = slider.Name;
                existed_slider.Description = slider.Description;
                existed_slider.Status = slider.Status;

                _dataContext.Update(existed_slider);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Slider updated successfully";
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
            SliderModel slider = await _dataContext.Sliders.FindAsync(Id);
            if (slider == null)
            {
                return NotFound();
            }


            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/sliders");
            string oldfilePath = Path.Combine(uploadDir, slider.Image);
            try
            {
                if (System.IO.File.Exists(oldfilePath))
                {
                    System.IO.File.Delete(oldfilePath);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while deleting the product image.");
            }

            _dataContext.Sliders.Remove(slider);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Slider removed successfully";
            return RedirectToAction("Index");
        }
    }
}
