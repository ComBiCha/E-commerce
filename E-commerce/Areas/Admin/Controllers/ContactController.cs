using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = "Admin")]
	public class ContactController : Controller
	{
		private readonly DataContext _dataContext;
		private readonly IWebHostEnvironment _webHostEnvironment;
		public ContactController(DataContext context, IWebHostEnvironment webHostEnvironment)
		{
			_dataContext = context;
			_webHostEnvironment = webHostEnvironment;
		}
		public IActionResult Index()
		{
			var contact = _dataContext.Contacts.ToList();
			return View(contact);
		}
        public async Task<IActionResult> Edit()
        {
            ContactModel contact = await _dataContext.Contacts.FirstOrDefaultAsync();
            ViewBag.OldImage = contact.LogoImg;
            return View(contact);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ContactModel contact)
        {
            var existed_contact = _dataContext.Contacts.FirstOrDefault();

            if (ModelState.IsValid)
            {


                if (contact.ImageUpload != null)
                {


                    string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/contact");
                    string imageName = Guid.NewGuid().ToString() + "_" + contact.ImageUpload.FileName;
                    string filePath = Path.Combine(uploadDir, imageName);

                    string oldfilePath = Path.Combine(uploadDir, existed_contact.LogoImg);
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
                    await contact.ImageUpload.CopyToAsync(fs);
                    fs.Close();
                    existed_contact.LogoImg = imageName;


                }
                existed_contact.Name = contact.Name;
                existed_contact.Description = contact.Description;
                existed_contact.Phone = contact.Phone;
                existed_contact.Email = contact.Email;
                existed_contact.Map = contact.Map;

                _dataContext.Update(existed_contact);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Contact updated successfully";
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
