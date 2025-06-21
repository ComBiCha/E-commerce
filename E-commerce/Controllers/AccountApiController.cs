using E_commerce.Models;
using E_commerce.Models.ViewModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly SignInManager<AppUserModel> _signInManager;
        private UserManager<AppUserModel> _userManager;

        public AccountApiController(SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginVM)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data");

            var result = await _signInManager.PasswordSignInAsync(loginVM.UserName, loginVM.Password, false, false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(loginVM.UserName);
                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    user = new
                    {
                        id = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        points = user.Points,
                        membershipTier = GetMembershipTier(user.Points),
                        discountRate = user.GetDiscountRate()
                    }
                });
            }
            return Unauthorized(new { success = false, message = "Invalid Username or Password" });
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] UserModel user)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data" });

            // Kiểm tra email
            var existingEmail = await _userManager.FindByEmailAsync(user.Email);
            if (existingEmail != null)
                return BadRequest(new { success = false, message = "Email đã được sử dụng." });

            // Kiểm tra số điện thoại
            var existingPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == user.PhoneNumber);
            if (existingPhone != null)
                return BadRequest(new { success = false, message = "Số điện thoại đã được sử dụng." });

            // Tạo user mới
            AppUserModel newUser = new AppUserModel
            {
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Points = 0 // Khởi tạo với 0 điểm
            };

            var result = await _userManager.CreateAsync(newUser, user.Password);

            if (result.Succeeded)
            {
                var addToRoleResult = await _userManager.AddToRoleAsync(newUser, "User");
                if (addToRoleResult.Succeeded)
                    return Ok(new { success = true, message = "Account created successfully" });

                return BadRequest(new { success = false, message = string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)) });
            }
            else
            {
                return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
        }

        // 🔹 CẬP NHẬT API GETUSERINFO ĐỂ BAO GỒM POINTS VÀ DISCOUNT RATE
        [HttpGet("GetUserInfo")]
        public async Task<IActionResult> GetUserInfo(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user != null)
                {
                    return Ok(new
                    {
                        success = true,
                        user = new
                        {
                            id = user.Id,
                            userName = user.UserName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            emailConfirmed = user.EmailConfirmed,
                            points = user.Points,
                            membershipTier = GetMembershipTier(user.Points),
                            discountRate = user.GetDiscountRate() // Trả về tỷ lệ giảm giá membership
                        }
                    });
                }

                return NotFound(new { success = false, message = "User not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API LẤY THÔNG TIN USER THEO EMAIL
        [HttpGet("GetUserByEmail")]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    return Ok(new
                    {
                        success = true,
                        user = new
                        {
                            id = user.Id,
                            userName = user.UserName,
                            email = user.Email,
                            phoneNumber = user.PhoneNumber,
                            emailConfirmed = user.EmailConfirmed,
                            points = user.Points,
                            membershipTier = GetMembershipTier(user.Points),
                            discountRate = user.GetDiscountRate()
                        }
                    });
                }

                return NotFound(new { success = false, message = "User not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 HELPER METHOD ĐỂ TÍNH MEMBERSHIP TIER
        private string GetMembershipTier(int points)
        {
            if (points >= 1000) return "Gold";
            if (points >= 500) return "Silver";
            if (points >= 100) return "Bronze";
            return "Basic";
        }
    }
}