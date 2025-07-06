using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserFacade _userFacade;

        public UserController(UserFacade userFacade)
        {
            _userFacade = userFacade;
        }

        public async Task<IActionResult> Index(int pg = 1)
        {
            // Chỉ lấy 1 danh sách
            var usersWithRoles = await _userFacade.GetUsersWithRolesAsync();

            const int pageSize = 5;
            if (pg < 1) pg = 1;

            // Dùng chung usersWithRoles cho pagination
            int recsCount = usersWithRoles.Count;
            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize;
            var pagedUsersWithRoles = usersWithRoles.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;
            var viewModel = new CombinedViewModel
            {
                UsersWithRoles = pagedUsersWithRoles, // Dùng data đã phân trang
                Users = null // Không cần nữa
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var roles = await _userFacade.GetAllRolesAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
            return View(new AppUserModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppUserModel user)
        {
            if (ModelState.IsValid)
            {
                var result = await _userFacade.CreateUserAsync(user, user.RoleId);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            var roles = await _userFacade.GetAllRolesAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _userFacade.DeleteUserAsync(id);
            if (result.Succeeded)
            {
                TempData["success"] = "User deleted successfully";
            }
            else
            {
                TempData["error"] = result.Errors.FirstOrDefault()?.Description ?? "Failed to delete user";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userFacade.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userFacade.GetRolesAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AppUserModel user)
        {
            if (!ModelState.IsValid)
            {
                var roles = await _userFacade.GetRolesAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
                return View(user);
            }

            try
            {
                var result = await _userFacade.UpdateUserWithRoleAsync(id, user);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            var rolesList = await _userFacade.GetRolesAsync();
            ViewBag.Roles = new SelectList(rolesList, "Id", "Name");
            return View(user);
        }
    }

}
