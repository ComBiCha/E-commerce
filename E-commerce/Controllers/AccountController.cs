using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Controllers
{
    public class AccountController : Controller
    {
        private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        public readonly DataContext _dataContext;
        public readonly IEmailSender _emailSender;
        public AccountController(SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager, DataContext dataContext, IEmailSender emailSender) 
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dataContext = dataContext;
            _emailSender = emailSender;
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
        public async Task<IActionResult> NewPassword(AppUserModel user, string token)
        {
            var checkUser = await _userManager.Users.Where(u => u.Email == user.Email).Where(u => u.Token == user.Token).FirstOrDefaultAsync();
            if (checkUser != null)
            {
                ViewBag.Email = checkUser.Email;
                ViewBag.Token = token;
            }
            else
            {
                TempData["error"] = "Email not found or token is not right";
                return RedirectToAction("ForgotPassword", "Account");
            }
            return View();
        }
        public async Task<IActionResult> SendEmailForgotPassword(AppUserModel user)
        {
            var checkMail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (checkMail == null)
            {
                TempData["error"] = "Email not found";
                return RedirectToAction("ForgotPassword", "Account");
            }
            else
            {
                string token = Guid.NewGuid().ToString();
                //update token to user
                checkMail.Token = token;
                _dataContext.Update(checkMail);
                await _dataContext.SaveChangesAsync();
                //update token to user
                var receiver = checkMail.Email;
                var subject = "Change password for user " + checkMail.Email;
                var message = "Click on link to change password " + "<a href='" + $"{Request.Scheme}://{Request.Host}/Account/NewPassword" + $"?email=" + checkMail.Email + "&token=" + token + "'>";
                await _emailSender.SendEmailAsync(receiver, subject, message);
            }
            TempData["success"] = "An email has been send to your registered email address with password reset instructions.";
            return RedirectToAction("ForgotPassword", "Account");
        }

        public async Task<IActionResult> ForgotPassword()
        {
            return View();
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
        [HttpPost]
        public async Task<IActionResult> UpdateNewPassword(AppUserModel user)
        {
            var checkUser = await _userManager.Users
                .Where(u => u.Email == user.Email)
                .Where(u => u.Token == user.Token)
                .FirstOrDefaultAsync();

            if (checkUser != null)
            {
                string newToken = Guid.NewGuid().ToString();
                var passwordHasher = new PasswordHasher<AppUserModel>();
                var passwordHash = passwordHasher.HashPassword(checkUser, user.PasswordHash);

                checkUser.PasswordHash = passwordHash;
                checkUser.Token = newToken;

                var result = await _userManager.UpdateAsync(checkUser);

                if (result.Succeeded)
                {
                    TempData["success"] = "Password updated successfully!";
                    return RedirectToAction("Login", "Account");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    TempData["error"] = "Password update failed.";
                    return RedirectToAction("ForgotPassword", "Account");
                }
            }
            else
            {
                TempData["error"] = "Email not found or token is incorrect.";
                return RedirectToAction("ForgotPassword", "Account");
            }
        }
    }
}
