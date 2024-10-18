using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Security.Claims;

namespace E_commerce.Controllers
{
    public class AccountController : Controller
    {
		private readonly DataContext _dataContext;
		private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        public AccountController(SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager, DataContext context) 
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dataContext = context;
        }
        public IActionResult Login(string returnUrl)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl});
        }
		[HttpPost]
		public async Task<IActionResult> Login(LoginViewModel loginVM)
        {
            if (ModelState.IsValid)
            {
                Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(loginVM.UserName, loginVM.Password, false, false);
                if(result.Succeeded)
                {
                    return Redirect(loginVM.ReturnUrl ?? "/");
                }
                ModelState.AddModelError("", "Invalid Username or Password");
            }
            return View(loginVM);
        }
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
		public async Task<IActionResult> Create(UserModel user)
		{
            if (ModelState.IsValid)
            {
                AppUserModel newUser = new AppUserModel { UserName= user.UserName, Email= user.Email};
                IdentityResult result = await _userManager.CreateAsync(newUser,user.Password);
                if (result.Succeeded)
                {
                    TempData["success"] = "Create account successfully";
                    return Redirect("/Account/Login");
                }
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("",error.Description);
                }
            }
			return View(user);
		}
        public async Task<IActionResult> Logout(string returnUrl = "/")
        {
            await _signInManager.SignOutAsync();
            return Redirect(returnUrl);
        }
		public async Task<IActionResult> Portal()
		{
			// Step 1: Get the current user's email
			var email = User.FindFirstValue(ClaimTypes.Email);

			// Step 2: Ensure email is not null
			if (string.IsNullOrEmpty(email))
			{
				return NotFound("User is not logged in or no email found.");
			}

			// Step 3: Get the current user using the UserManager
			var currentUser = await _userManager.FindByEmailAsync(email);
			if (currentUser == null)
			{
				return NotFound("User not found.");
			}
			// Step 6: Return the view with the model
			return View(currentUser);
		}

        public async Task<IActionResult> PersonalOrder()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return NotFound("User is not logged in or no email found.");
            }
            var check = _dataContext.Orders
                        .Where(d => d.UserName == email)
                        .OrderBy(c => c.CreatedDate);
            return View(await check.OrderBy(p => p.CreatedDate).ToListAsync());
        }

        public async Task<IActionResult> ViewOrder(string ordercode)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == ordercode);
            ViewBag.Order = order;

            var DetailsOrder = await _dataContext.OrderDetails.Include(o => o.Product).Where(o => o.OrderCode == ordercode).ToListAsync();
            return View(DetailsOrder);
        }
    }
}
