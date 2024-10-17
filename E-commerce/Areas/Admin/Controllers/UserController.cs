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
        private readonly UserManager<AppUserModel> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;
		public UserController(DataContext context,UserManager<AppUserModel> userManager,RoleManager<IdentityRole> roleManager)
        {
            _dataContext = context;
            _userManager = userManager;
            _roleManager = roleManager;
		}
        /*public async Task<IActionResult> Index()
        {
            var usersWithRoles = await (from u in _dataContext.Users
                                        join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                        join r in _dataContext.Roles on ur.RoleId equals r.Id
                                        select new { User = u, RoleName = r.Name }).ToListAsync();
            return View(usersWithRoles);
        }
        public async Task<IActionResult> Index(int pg = 1)
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
        public async Task<IActionResult> Index(int pg = 1)
        {
            // Step 1: Retrieve users and their roles
            var usersWithRoles = await (from u in _dataContext.Users
                                        join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                        join r in _dataContext.Roles on ur.RoleId equals r.Id
                                        select new { User = u, RoleName = r.Name }).ToListAsync();

            // Step 2: Retrieve categories and paginate them
            List<AppUserModel> user = _dataContext.Users.ToList(); // Assume 33 datas

            const int pageSize = 10; // Items per page
            if (pg < 1)
            {
                pg = 1; // Ensuring that page is at least 1
            }

            int recsCount = user.Count(); // Total categories count (33 items in this case)

            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize; // Calculate how many records to skip

            // Paginate categories
            var data = user.Skip(recSkip).Take(pager.PageSize).ToList();

            // Step 3: Pass both results to the View using a ViewModel
            ViewBag.Pager = pager;

            // Create a combined ViewModel to pass both sets of data
            var viewModel = new CombinedViewModel
            {
                UsersWithRoles = usersWithRoles,
                Users = data
            };

            return View(viewModel);
        }

        [HttpGet]
		public async Task<IActionResult> Create()
		{
            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
			return View(new AppUserModel());
		}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppUserModel user)
        {
            if (ModelState.IsValid)
            {
                var createUserResult = await _userManager.CreateAsync(user,user.PasswordHash);
                if(createUserResult.Succeeded)
                {
                    var createUser = await _userManager.FindByEmailAsync(user.Email);
                    var userId = createUser.Id;
                    var role = _roleManager.FindByIdAsync(user.RoleId);

                    var addToRoleResult = await _userManager.AddToRoleAsync(createUser, role.Result.Name);
                    if (!addToRoleResult.Succeeded)
                    {
                        AddIdentityErrors(addToRoleResult);
                    }
                    return RedirectToAction("Index", "User");
                }
                else
                {
                    AddIdentityErrors(createUserResult);
                    return View(user);
                }
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
            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
            return View(user);
        }
        private void AddIdentityErrors(IdentityResult identityResult)
        {
            foreach(var error in identityResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Delete(string Id)
        {
            if(string.IsNullOrEmpty(Id))
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(Id);
            if(user == null)
            {
                return NotFound();
            }
            var deleteResult = await _userManager.DeleteAsync(user);
            if(!deleteResult.Succeeded)
            {
                return View("Error");
            }
            TempData["success"] = "User deleted successfully";
            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");

            return View(user);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AppUserModel user)
        {
            var existingUser = await _userManager.FindByIdAsync(id);
            if(existingUser == null)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                existingUser.UserName = user.UserName;
                existingUser.Email = user.Email;
                existingUser.PhoneNumber = user.PhoneNumber;
                existingUser.RoleId = user.RoleId;

                var updateUserResult = await _userManager.UpdateAsync(existingUser);
                if (updateUserResult.Succeeded)
                {
                    return RedirectToAction("Index", "User");
                }
                else
                {
                    AddIdentityErrors(updateUserResult);
                    return View(existingUser);
                }
            }
            else
            {
                var roles = await _roleManager.Roles.ToListAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
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
            
            return View(user);
        }
    }
}
