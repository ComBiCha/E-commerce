using E_commerce.Models;
using E_commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RoleController : Controller
    {
        private readonly RoleManagerDecorator _roleManager;

        public RoleController(RoleManagerDecorator roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(int pg = 1)
        {
            const int pageSize = 10;
            if (pg < 1) pg = 1;

            var roles = await _roleManager.GetAllRolesAsync();
            int recsCount = roles.Count;
            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize;
            var data = roles.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;
            return View(data);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IdentityRole role)
        {
            var result = await _roleManager.CreateRoleAsync(role.Name);
            if (result.Succeeded)
            {
                TempData["success"] = "Role added successfully!";
                return RedirectToAction("Index");
            }

            ViewData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View(role);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, IdentityRole model)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            if (ModelState.IsValid)
            {
                role.Name = model.Name;
                var result = await _roleManager.UpdateRoleAsync(role);

                if (result.Succeeded)
                {
                    TempData["success"] = "Role updated successfully!";
                    return RedirectToAction("Index");
                }

                ViewData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật role!";
            }

            return View(model);
        }

        public async Task<IActionResult> Delete(string id)
        {
            Console.WriteLine($"ID Role cần xóa: {id}");
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                Console.WriteLine("Không tìm thấy Role!");
                return NotFound();
            }

            var result = await _roleManager.DeleteRoleAsync(role);
            if (result.Succeeded)
            {
                Console.WriteLine("Xóa Role thành công!");
                TempData["success"] = "Role deleted successfully!";
            }
            else
            {
                Console.WriteLine("Lỗi khi xóa Role!");
                TempData["error"] = "Có lỗi xảy ra khi xóa Role!";
            }

            return RedirectToAction("Index");
        }



    }
}
