
﻿using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
			return View(new LoginViewModel { ReturnUrl = returnUrl });
		}

		[HttpPost]
		public async Task<IActionResult> Login(LoginViewModel loginVM)
		{
			if (ModelState.IsValid)
			{
				Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(loginVM.UserName, loginVM.Password, false, false);
				if (result.Succeeded)
				{
					return Redirect(loginVM.ReturnUrl ?? "/");
				}
				ModelState.AddModelError("", "Invalid Username or Password");
			}
			return View(loginVM);
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

		public async Task<IActionResult> NewPassword(AppUserModel user, string token)
		{
			var checkUser = await _userManager.Users
				.Where(u => u.Email == user.Email)
				.Where(u => u.Token == user.Token)
				.FirstOrDefaultAsync();

			if (checkUser != null)
			{
				ViewBag.Email = checkUser.Email;
				ViewBag.Token = token;
			}
			else
			{
				TempData["error"] = "Email not found or token is incorrect.";
				return RedirectToAction("ForgotPassword", "Account");
			}
			return View();
		}

		public async Task<IActionResult> SendEmailForgotPassword(AppUserModel user)
		{
			var checkMail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == user.Email);

			if (checkMail == null)
			{
				TempData["error"] = "Email not found.";
				return RedirectToAction("ForgotPassword", "Account");
			}
			else
			{
				string token = Guid.NewGuid().ToString();
				// Update token for user
				checkMail.Token = token;
				_dataContext.Update(checkMail);
				await _dataContext.SaveChangesAsync();

				// Send reset email
				var receiver = checkMail.Email;
				var subject = "Change password for user " + checkMail.Email;
				var message = $"Click on this link to change your password: <a href='{Request.Scheme}://{Request.Host}/Account/NewPassword?email={checkMail.Email}&token={token}'>Change Password</a>";

				await _emailSender.SendEmailAsync(receiver, subject, message);
			}

			TempData["success"] = "An email has been sent to your registered email address with password reset instructions.";
			return RedirectToAction("ForgotPassword", "Account");
		}

		public IActionResult ForgotPassword()
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
                AppUserModel newUser = new AppUserModel
                {
                    UserName = user.UserName,
                    Email = user.Email
                };

                IdentityResult result = await _userManager.CreateAsync(newUser, user.Password);

                if (result.Succeeded)
                {
                    // Assign the default role "User" to the newly created account
                    var addToRoleResult = await _userManager.AddToRoleAsync(newUser, "User");

                    if (addToRoleResult.Succeeded)
                    {
                        TempData["success"] = "Account created successfully";
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        // Handle any errors that occurred while adding the user to the role
                        foreach (IdentityError error in addToRoleResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
                else
                {
                    // Handle errors during account creation
                    foreach (IdentityError error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
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
            return View(await check.OrderByDescending(p => p.CreatedDate).ToListAsync());
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
