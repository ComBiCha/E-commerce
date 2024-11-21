
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
            var DetailsOrder = await _dataContext.OrderDetails
        .Include(o => o.Product)
        .ThenInclude(p => p.Warranty) // Bao gồm thông tin bảo hành từ Product
        .Where(o => o.OrderCode == ordercode)
        .ToListAsync();
            return View(DetailsOrder);
        }
        public async Task<IActionResult> MyWarranties()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Lấy User ID từ Claims
            if (string.IsNullOrEmpty(userId))
            {
                TempData["error"] = "Unable to identify the user. Please log in again.";
                return RedirectToAction("Login", "Account"); // Chuyển hướng đến trang đăng nhập nếu không lấy được userId
            }

            var myRequests = await _dataContext.WarrantyRequests
                .Where(wr => wr.UserId == userId)
                .Include(wr => wr.Warranty)
                .ThenInclude(w => w.Product) // Bao gồm thông tin sản phẩm (nếu cần hiển thị trong View)
                .ToListAsync();

            return View(myRequests);
        }

        public async Task<IActionResult> WarrantyDetails(int id)
        {
            var request = await _dataContext.WarrantyRequests
                .Include(r => r.Warranty)
                .Include(r => r.User)
                .Include(r => r.Warranty.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }
        public async Task<IActionResult> CancelWarranty(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Lấy UserId của người dùng

            if (string.IsNullOrEmpty(userId))
            {
                TempData["error"] = "Unable to identify the user. Please log in again.";
                return RedirectToAction("Login", "Account"); // Chuyển hướng đến trang đăng nhập nếu không lấy được userId
            }

            var request = await _dataContext.WarrantyRequests
                .FirstOrDefaultAsync(wr => wr.Id == id && wr.UserId == userId);

            if (request == null)
            {
                TempData["error"] = "Không tìm thấy yêu cầu bảo hành này.";
                return RedirectToAction("MyWarranties");
            }

            // Cập nhật trạng thái yêu cầu bảo hành thành "Đã huỷ"
            request.Status = 6;
            request.UpdatedDate = DateTime.Now;

            _dataContext.WarrantyRequests.Update(request);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Yêu cầu bảo hành đã được huỷ thành công.";
            return RedirectToAction("MyWarranties");
        }
        public async Task<IActionResult> CancelOrder(string orderCode)
        {
            // Lấy Email của người dùng hiện tại từ Claims
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                TempData["error"] = "Unable to identify the user. Please log in again.";
                return RedirectToAction("Login", "Account"); // Nếu không đăng nhập, chuyển hướng về Login
            }

            // Kiểm tra đơn hàng có tồn tại và thuộc về người dùng hiện tại không
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == email);

            if (order == null)
            {
                TempData["error"] = "Order not found or you do not have permission to cancel this order.";
                return RedirectToAction("PersonalOrder"); // Chuyển hướng về danh sách đơn hàng cá nhân
            }


            // Cập nhật trạng thái đơn hàng thành "Đã hủy"
            order.Status = 6;

            _dataContext.Orders.Update(order);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Order has been canceled successfully.";
            return RedirectToAction("PersonalOrder"); // Chuyển hướng về danh sách đơn hàng cá nhân
        }
        [HttpPost]
        public async Task<IActionResult> ConfirmDelivery(string orderCode)
        {
            // Lấy email của người dùng từ Claims
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                TempData["error"] = "Unable to identify the user. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Tìm đơn hàng theo mã và người dùng
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == email);

            if (order == null)
            {
                TempData["error"] = "Order not found or you do not have permission to confirm this delivery.";
                return RedirectToAction("PersonalOrder"); // Chuyển hướng về danh sách đơn hàng cá nhân
            }

            // Kiểm tra xem đơn hàng có ở trạng thái "Hàng đã giao tới nơi" (4) không
            if (order.Status == 4)
            {
                order.Status = 5;  // Cập nhật trạng thái thành "Đơn hàng đã hoàn thành"
                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Order has been confirmed as received.";
            }
            else
            {
                TempData["error"] = "This order cannot be confirmed at this stage.";
            }

            return RedirectToAction("PersonalOrder"); // Chuyển hướng về danh sách đơn hàng cá nhân
        }


    }
}
