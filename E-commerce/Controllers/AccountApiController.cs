using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
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
        private readonly DataContext _dataContext;

        public AccountApiController(SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager, DataContext dataContext)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dataContext = dataContext;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginVM)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid data" });

                var result = await _signInManager.PasswordSignInAsync(loginVM.UserName, loginVM.Password, false, false);
                if (result.Succeeded)
                {
                    var user = await _userManager.Users
                        .Include(u => u.Addresses)
                        .FirstOrDefaultAsync(u => u.UserName == loginVM.UserName);

                    if (user == null)
                        return NotFound(new { success = false, message = "User not found" });

                    // 🔹 LẤY ĐẦY ĐỦ THỐNG KÊ NGAY TRONG LOGIN
                    var orderCount = await _dataContext.Orders.CountAsync(o => o.UserName == user.Email);
                    var wishlistCount = await _dataContext.Wishlists.CountAsync(w => w.UserId == user.Id);
                    var shippingCount = await _dataContext.Orders.CountAsync(o => o.UserName == user.Email && o.Status == 3);

                    // 🔹 SỬA LẠI: Tạo addresses list với kiểu dữ liệu đúng
                    var addresses = user.Addresses?.Select(a => new
                    {
                        id = a.Id,
                        fullName = a.FullName,
                        phoneNumber = a.PhoneNumber,
                        city = a.City,
                        district = a.District,
                        ward = a.Ward,
                        detailAddress = a.DetailAddress,
                        fullAddress = a.GetFullAddress(),
                        isDefault = a.IsDefault
                    }).ToList();

                    var userData = new
                    {
                        id = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        avatar = user.Avatar ?? "https://via.placeholder.com/100",
                        points = user.Points,
                        membershipTier = GetMembershipTier(user.Points),
                        discountRate = user.GetDiscountRate(),
                        orderCount = orderCount,
                        wishlistCount = wishlistCount,
                        shippingCount = shippingCount,
                        defaultAddress = user.Addresses?.FirstOrDefault(a => a.IsDefault)?.GetFullAddress(),
                        addresses = addresses // 🔹 SỬA LẠI: Không dùng ?? operator
                    };

                    return Ok(new
                    {
                        success = true,
                        message = "Login successful",
                        user = userData
                    });
                }
                return Unauthorized(new { success = false, message = "Invalid Username or Password" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Login error: {ex.Message}" });
            }
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
                {
                    // 🔹 TRẢ VỀ ĐẦY ĐỦ THÔNG TIN USER SAU KHI ĐĂNG KÝ
                    var userData = new
                    {
                        id = newUser.Id,
                        userName = newUser.UserName,
                        email = newUser.Email,
                        phoneNumber = newUser.PhoneNumber,
                        avatar = newUser.Avatar ?? "https://via.placeholder.com/100",
                        points = newUser.Points,
                        membershipTier = GetMembershipTier(newUser.Points),
                        discountRate = newUser.GetDiscountRate(),
                        orderCount = 0,
                        wishlistCount = 0,
                        shippingCount = 0,
                        addresses = new object[0] // 🔹 SỬA LẠI: Dùng empty array thay vì List<object>
                    };

                    return Ok(new
                    {
                        success = true,
                        message = "Account created successfully",
                        user = userData
                    });
                }

                return BadRequest(new { success = false, message = string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)) });
            }
            else
            {
                return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
        }

        // 🔹 API LẤY THÔNG TIN PROFILE ĐẦY ĐỦ
        [HttpGet("GetProfile")]
        public async Task<IActionResult> GetProfile(string email)
        {
            try
            {
                Console.WriteLine($"GetProfile called with email: {email}");

                var user = await _userManager.Users
                    .Include(u => u.Addresses)
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    Console.WriteLine("User not found");
                    return NotFound(new { success = false, message = "User not found" });
                }

                Console.WriteLine($"Found user: {user.UserName}");

                // 🔹 SỬA LẠI: Sử dụng UserId thay vì UserName cho Wishlists
                var orderCount = await _dataContext.Orders.CountAsync(o => o.UserName == email);
                var wishlistCount = await _dataContext.Wishlists.CountAsync(w => w.UserId == user.Id);
                var shippingCount = await _dataContext.Orders.CountAsync(o => o.UserName == email && o.Status == 3);

                // 🔹 SỬA LẠI: Tạo addresses list với kiểu dữ liệu đúng
                var addresses = user.Addresses?.Select(a => new
                {
                    id = a.Id,
                    fullName = a.FullName,
                    phoneNumber = a.PhoneNumber,
                    city = a.City,
                    district = a.District,
                    ward = a.Ward,
                    detailAddress = a.DetailAddress,
                    fullAddress = a.GetFullAddress(),
                    isDefault = a.IsDefault
                }).ToList();

                var result = new
                {
                    success = true,
                    user = new
                    {
                        id = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        avatar = user.Avatar ?? "https://via.placeholder.com/100",
                        points = user.Points,
                        membershipTier = GetMembershipTier(user.Points),
                        discountRate = user.GetDiscountRate(),
                        orderCount = orderCount,
                        wishlistCount = wishlistCount,
                        shippingCount = shippingCount,
                        addresses = addresses // 🔹 SỬA LẠI: Không dùng ?? operator
                    }
                };

                Console.WriteLine($"Returning result: {System.Text.Json.JsonSerializer.Serialize(result)}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetProfile error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ... rest of the methods remain the same

        // 🔹 API CẬP NHẬT AVATAR
        [HttpPost("UpdateAvatar")]
        public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                user.Avatar = request.AvatarUrl;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Ok(new { success = true, message = "Avatar updated successfully" });
                }

                return BadRequest(new { success = false, message = "Failed to update avatar" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Thêm vào AccountApiController.cs:

        [HttpPost("UploadAvatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar, [FromForm] string email)
        {
            try
            {
                if (avatar == null || avatar.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                // Tạo thư mục avatars nếu chưa có
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "media", "avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Tạo tên file unique
                var fileName = $"user_{DateTime.Now.Ticks}{Path.GetExtension(avatar.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Lưu file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                // Tạo URL
                var avatarUrl = $"{Request.Scheme}://{Request.Host}/media/avatars/{fileName}";

                // Cập nhật database
                user.Avatar = avatarUrl;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Avatar uploaded successfully",
                        avatarUrl = avatarUrl
                    });
                }

                return BadRequest(new { success = false, message = "Failed to update avatar" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API THÊM ĐỊA CHỈ MỚI
        [HttpPost("AddAddress")]
        public async Task<IActionResult> AddAddress([FromBody] AddAddressRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                // Nếu đặt làm default, bỏ default của địa chỉ khác
                if (request.IsDefault)
                {
                    var existingAddresses = await _dataContext.UserAddresses
                        .Where(a => a.UserId == user.Id)
                        .ToListAsync();

                    foreach (var addr in existingAddresses)
                    {
                        addr.IsDefault = false;
                    }
                }

                var newAddress = new UserAddressModel
                {
                    UserId = user.Id,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    City = request.City,
                    District = request.District,
                    Ward = request.Ward,
                    DetailAddress = request.DetailAddress,
                    IsDefault = request.IsDefault
                };

                _dataContext.UserAddresses.Add(newAddress);
                await _dataContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Address added successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API LẤY DANH SÁCH ĐỊA CHỈ
        [HttpGet("GetUserAddresses")]
        public async Task<IActionResult> GetUserAddresses(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var addresses = await _dataContext.UserAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedDate)
                    .Select(a => new
                    {
                        id = a.Id,
                        fullName = a.FullName,
                        phoneNumber = a.PhoneNumber,
                        city = a.City,
                        district = a.District,
                        ward = a.Ward,
                        detailAddress = a.DetailAddress,
                        fullAddress = a.GetFullAddress(),
                        isDefault = a.IsDefault,
                        createdDate = a.CreatedDate
                    })
                    .ToListAsync();

                return Ok(new { success = true, addresses = addresses });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API CẬP NHẬT ĐỊA CHỈ
        [HttpPut("UpdateAddress")]
        public async Task<IActionResult> UpdateAddress([FromBody] UpdateAddressRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var address = await _dataContext.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == user.Id);

                if (address == null)
                    return NotFound(new { success = false, message = "Address not found" });

                // Nếu đặt làm default, bỏ default của địa chỉ khác
                if (request.IsDefault && !address.IsDefault)
                {
                    var existingAddresses = await _dataContext.UserAddresses
                        .Where(a => a.UserId == user.Id && a.Id != request.Id)
                        .ToListAsync();

                    foreach (var addr in existingAddresses)
                    {
                        addr.IsDefault = false;
                    }
                }

                // Cập nhật thông tin địa chỉ
                address.FullName = request.FullName;
                address.PhoneNumber = request.PhoneNumber;
                address.City = request.City;
                address.District = request.District;
                address.Ward = request.Ward;
                address.DetailAddress = request.DetailAddress;
                address.IsDefault = request.IsDefault;

                await _dataContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Address updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API XÓA ĐỊA CHỈ
        [HttpDelete("DeleteAddress")]
        public async Task<IActionResult> DeleteAddress(int id, string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var address = await _dataContext.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

                if (address == null)
                    return NotFound(new { success = false, message = "Address not found" });

                _dataContext.UserAddresses.Remove(address);
                await _dataContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Address deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API ĐẶT ĐỊA CHỈ MẶC ĐỊNH
        [HttpPost("SetDefaultAddress")]
        public async Task<IActionResult> SetDefaultAddress([FromBody] SetDefaultAddressRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var address = await _dataContext.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == user.Id);

                if (address == null)
                    return NotFound(new { success = false, message = "Address not found" });

                // Bỏ default của các địa chỉ khác
                var existingAddresses = await _dataContext.UserAddresses
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                foreach (var addr in existingAddresses)
                {
                    addr.IsDefault = (addr.Id == request.AddressId);
                }

                await _dataContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Default address updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Thêm vào AccountApiController.cs:

        [HttpPost("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                Console.WriteLine($"UpdateProfile called with email: {request.Email}");

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                // Cập nhật thông tin
                user.UserName = request.UserName;
                user.PhoneNumber = request.PhoneNumber;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    Console.WriteLine("Profile updated successfully");
                    return Ok(new { success = true, message = "Profile updated successfully" });
                }

                return BadRequest(new
                {
                    success = false,
                    message = "Failed to update profile",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateProfile error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private string GetMembershipTier(int points)
        {
            if (points >= 50000) return "Diamond";
            if (points >= 40000) return "Platinum";
            if (points >= 20000) return "Gold";
            if (points >= 10000) return "Silver";
            return "Bronze";
        }
    }

    // Thêm request model vào cuối file:
    public class UpdateProfileRequest
    {
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
    }

    // Request models
    public class UpdateAvatarRequest
    {
        public string Email { get; set; }
        public string AvatarUrl { get; set; }
    }

    public class AddAddressRequest
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public string DetailAddress { get; set; }
        public bool IsDefault { get; set; }
    }

    public class UpdateAddressRequest
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public string DetailAddress { get; set; }
        public bool IsDefault { get; set; }
    }

    public class SetDefaultAddressRequest
    {
        public int AddressId { get; set; }
        public string Email { get; set; }
    }
}