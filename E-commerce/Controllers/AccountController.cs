using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using E_commerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayPal.Api;
using Stripe;
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
				// Kiểm tra xem Email đã tồn tại chưa
				var existingEmail = await _userManager.FindByEmailAsync(user.Email);
				if (existingEmail != null)
				{
					ModelState.AddModelError("Email", "Email này đã được sử dụng.");
					return View(user);
				}

				// Kiểm tra xem số điện thoại đã tồn tại chưa
				var existingPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == user.PhoneNumber);
				if (existingPhone != null)
				{
					ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được sử dụng.");
					return View(user);
				}

				// Tạo user mới
				AppUserModel newUser = new AppUserModel
				{
					UserName = user.UserName,
					Email = user.Email,
					PhoneNumber = user.PhoneNumber,
/*					Address = user.Address*/
				};

				IdentityResult result = await _userManager.CreateAsync(newUser, user.Password);

				if (result.Succeeded)
				{
					var addToRoleResult = await _userManager.AddToRoleAsync(newUser, "User");

					if (addToRoleResult.Succeeded)
					{
						TempData["success"] = "Account created successfully";
						return RedirectToAction("Login", "Account");
					}
					else
					{
						foreach (IdentityError error in addToRoleResult.Errors)
						{
							ModelState.AddModelError("", error.Description);
						}
					}
				}
				else
				{
					foreach (IdentityError error in result.Errors)
					{
						ModelState.AddModelError("", error.Description);
					}
				}
			}

			return View(user);
		}

        [HttpPost]
        public async Task<IActionResult> VerifyEmail()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Portal");
            }

            if (user.EmailConfirmed)
            {
                TempData["success"] = "Email Verified.";
                return RedirectToAction("Portal");
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token }, Request.Scheme);

            // Gửi email xác thực
            await _emailSender.SendEmailAsync(user.Email, "Verify Email",
                $"Please verify your email by <a href='{callbackUrl}'>Click here</a>.");

            TempData["success"] = "Email verification is sent.";
            return RedirectToAction("Portal");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Portal");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Portal");
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                TempData["success"] = "Email is successfully verified!";
            }
            else
            {
                TempData["error"] = "Error in verify email!";
            }

            return RedirectToAction("Portal");
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
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == ordercode);

            if (order == null)
            {
                return NotFound();
            }

            var userEmail = order.UserName;
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            decimal discountRate = user?.GetDiscountRate() ?? 0m; // Lấy mức giảm giá từ UserModel
            decimal productTotal = await _dataContext.OrderDetails
                .Where(o => o.OrderCode == ordercode)
                .SumAsync(o => o.Price * o.Quantity); // Tính tổng giá sản phẩm

            decimal discountAmount = productTotal * discountRate; // Số tiền giảm giá

            ViewBag.Order = order;
            ViewBag.DiscountRate = discountRate; // Gửi Discount Rate sang View
            ViewBag.DiscountAmount = discountAmount; // Số tiền giảm giá
            ViewBag.ProductTotal = productTotal;

            var DetailsOrder = await _dataContext.OrderDetails
                .Include(o => o.Product)
                    .ThenInclude(p => p.Warranty)
                .Include(o => o.Variation)
                .Include(o => o.Variation.Material)
                .Include(o => o.Variation.Color)
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
            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
            {
                TempData["error"] = "Order not found or you do not have permission to cancel this order.";
                return RedirectToAction("PersonalOrder");
            }

            // Nếu đơn hàng đã xử lý thì không cho hủy
            if (order.Status > 2)
            {
                TempData["error"] = "Order has already been processed and cannot be canceled.";
                return RedirectToAction("PersonalOrder");
            }

            var orderDetails = await _dataContext.OrderDetails
                .Include(od => od.Variation)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Variations)
                            .ThenInclude(v => v.ProductQuantities)
                .Where(od => od.OrderCode == orderCode)
                .ToListAsync();

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                foreach (var orderDetail in orderDetails)
                {
                    if (orderDetail.Variation != null)
                    {
                        int quantityToRestock = orderDetail.Quantity;

                        var productQuantities = await _dataContext.ProductQuantities
                            .Where(pq => pq.VariationId == orderDetail.Variation.Id)
                            .OrderByDescending(pq => pq.DateCreated)
                            .ToListAsync();

                        if (productQuantities.Any())
                        {
                            var latestBatch = productQuantities.First();
                            latestBatch.CurrentQuantityInBatch += quantityToRestock;
                            latestBatch.LastUpdated = DateTime.Now;
                            _dataContext.ProductQuantities.Update(latestBatch);
                        }
                        else
                        {
                            var newBatch = new BatchModel
                            {
                                BatchCode = $"RESTOCK-{orderCode}-{DateTime.Now.Ticks}",
                                ImportDate = DateTime.Now
                            };
                            _dataContext.Batches.Add(newBatch);
                            await _dataContext.SaveChangesAsync();

                            var newProductQuantity = new ProductQuantityModel
                            {
                                VariationId = orderDetail.Variation.Id,
                                BatchId = newBatch.Id,
                                InitialQuantity = quantityToRestock,
                                CurrentQuantityInBatch = quantityToRestock,
                                DateCreated = DateTime.Now,
                                LastUpdated = DateTime.Now
                            };
                            _dataContext.ProductQuantities.Add(newProductQuantity);
                        }
                    }
                }

                // Hoàn tiền nếu có
                if (!string.IsNullOrEmpty(order.PaymentIntentId))
                {
                    if (order.PaymentIntentId.StartsWith("pi_"))
                    {
                        await ProcessStripeRefund(order.PaymentIntentId);
                    }
                    else if (order.PaymentIntentId.StartsWith("PAYID-"))
                    {
                        await ProcessPayPalRefund(order.PaymentIntentId);
                    }
                }

                order.Status = 6; // Đã hủy
                _dataContext.Orders.Update(order);
                foreach (var orderDetail in orderDetails)
                {
                    var product = orderDetail.Variation?.Product;
                    if (product != null)
                    {
                        product.Sold = Math.Max(0, product.Sold - orderDetail.Quantity);
                        _dataContext.Products.Update(product);
                    }
                }
                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["success"] = "Order has been canceled successfully, stock has been restored, and refund request has been processed.";
                return RedirectToAction("Home");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["error"] = "Đã xảy ra lỗi khi hủy đơn hàng: " + ex.Message;
                return RedirectToAction("PersonalOrder");
            }
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

        private async Task ProcessStripeRefund(string paymentIntentId)
        {
            try
            {
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = paymentIntentId,
                    Reason = "requested_by_customer"
                };
                var refundService = new RefundService();
                var refund = await refundService.CreateAsync(refundOptions);

                if (refund.Status == "succeeded")
                {
                    TempData["success"] = "Stripe refund processed successfully.";
                }
                else
                {
                    TempData["error"] = "Stripe refund processing failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error processing Stripe refund: " + ex.Message;
            }
        }

        private async Task ProcessPayPalRefund(string paymentId)
        {
            try
            {
                var apiContext = new PayPalSdk().GetAPIContext(); // Lấy APIContext từ PayPalSdk

                // Lấy thông tin Payment từ PayPal
                var payment = Payment.Get(apiContext, paymentId);

                // Kiểm tra nếu không có giao dịch nào
                if (payment.transactions.Count == 0 || payment.transactions[0].related_resources.Count == 0)
                {
                    TempData["error"] = "No transactions found for this PayPal payment.";
                    return;
                }

                // Lấy Sale ID từ giao dịch đầu tiên
                var saleId = payment.transactions[0].related_resources[0].sale.id;

                // Tạo yêu cầu hoàn tiền
                var refundRequest = new RefundRequest
                {
                    amount = new Amount
                    {
                        total = payment.transactions[0].amount.total, // Tổng tiền hoàn
                        currency = payment.transactions[0].amount.currency // Đơn vị tiền tệ
                    }
                };

                // Gọi Refund API
                var refund = Sale.Refund(apiContext, saleId, refundRequest); // ✅ Gọi đúng cách

                if (refund.state.ToLower() == "completed")
                {
                    TempData["success"] = "PayPal refund processed successfully.";
                }
                else
                {
                    TempData["error"] = "PayPal refund processing failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error processing PayPal refund: " + ex.Message;
            }
        }

    }
}
