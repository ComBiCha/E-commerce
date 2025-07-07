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

        // 🔹 API LẤY DANH SÁCH ORDER CỦA USER
        [HttpGet("GetUserOrders")]
        public async Task<IActionResult> GetUserOrders(string email)
        {
            try
            {
                Console.WriteLine($"GetUserOrders called with email: {email}");

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var orders = await _dataContext.Orders
                    .Where(o => o.UserName == email)
                    .OrderByDescending(o => o.CreatedDate)
                    .Select(o => new
                    {
                        id = o.Id,
                        orderCode = o.OrderCode,
                        shippingCost = o.ShippingCost,
                        address = o.Address,
                        userName = o.UserName,
                        createdDate = o.CreatedDate,
                        status = o.Status,
                        statusName = GetOrderStatusName(o.Status),
                        paymentIntentId = o.PaymentIntentId,
                        paymentMethod = GetPaymentMethod(o.PaymentIntentId),
                        canCancel = o.Status <= 2, // Chỉ có thể hủy khi status <= 2
                                                   // Tính tổng tiền đơn hàng
                        totalAmount = _dataContext.OrderDetails
                            .Where(od => od.OrderCode == o.OrderCode)
                            .Sum(od => od.Price * od.Quantity - od.DiscountAmount) + o.ShippingCost,
                        // Đếm số sản phẩm
                        itemCount = _dataContext.OrderDetails
                            .Where(od => od.OrderCode == o.OrderCode)
                            .Sum(od => od.Quantity)
                    })
                    .ToListAsync();

                return Ok(new { success = true, orders = orders });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserOrders error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API LẤY CHI TIẾT ORDER
        [HttpGet("GetOrderDetails")]
        public async Task<IActionResult> GetOrderDetails(string orderCode, string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var order = await _dataContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == email);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found" });

                var orderDetails = await _dataContext.OrderDetails
                    .Include(od => od.Product)
                        .ThenInclude(p => p.Warranty)
                    .Include(od => od.Variation)
                        .ThenInclude(v => v.Material)
                    .Include(od => od.Variation)
                        .ThenInclude(v => v.Color)
                    .Where(od => od.OrderCode == orderCode)
                    .Select(od => new
                    {
                        id = od.Id,
                        productId = od.ProductId,
                        variationId = od.VariationId,
                        productName = od.Product.Name,
                        price = od.Price,                    // 🔹 Giá gốc
                        quantity = od.Quantity,
                        discountAmount = od.DiscountAmount,  // 🔹 Tổng discount cho item này
                        finalPrice = od.Price - (od.DiscountAmount / od.Quantity), // 🔹 Giá cuối cho 1 item
                        subtotal = od.Quantity * (od.Price - (od.DiscountAmount / od.Quantity)), // 🔹 Tổng tiền đã giảm
                        imageUrl = od.Variation.ImageUrl,
                        material = od.Variation.Material.Name,
                        color = od.Variation.Color.Name,
                        size = od.Variation.Size,
                        warrantyCode = od.Product.Warranty.FirstOrDefault().WarrantyCode,
                        warrantyExpirationDate = od.Product.Warranty.FirstOrDefault().ExpirationDate
                    })
                    .ToListAsync();

                var productTotal = orderDetails.Sum(od => od.subtotal);
                var grandTotal = productTotal + order.ShippingCost;

                var result = new
                {
                    success = true,
                    order = new
                    {
                        id = order.Id,
                        orderCode = order.OrderCode,
                        shippingCost = order.ShippingCost,
                        address = order.Address,
                        userName = order.UserName,
                        createdDate = order.CreatedDate,
                        status = order.Status,
                        statusName = GetOrderStatusName(order.Status),
                        paymentIntentId = order.PaymentIntentId,
                        paymentMethod = GetPaymentMethod(order.PaymentIntentId),
                        canCancel = order.Status <= 2,
                        productTotal = productTotal,
                        grandTotal = grandTotal
                    },
                    orderDetails = orderDetails
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetOrderDetails error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API HỦY ORDER
        [HttpPost("CancelOrder")]
        public async Task<IActionResult> CancelOrder([FromBody] CancelOrderRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var order = await _dataContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderCode == request.OrderCode && o.UserName == request.Email);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found or you do not have permission to cancel this order" });

                // Kiểm tra trạng thái có thể hủy
                if (order.Status > 2)
                    return BadRequest(new { success = false, message = "Order has already been processed and cannot be canceled" });

                // Lấy chi tiết đơn hàng để hoàn kho
                var orderDetails = await _dataContext.OrderDetails
                    .Include(od => od.Variation)
                        .ThenInclude(v => v.Product)
                            .ThenInclude(p => p.Variations)
                                .ThenInclude(v => v.ProductQuantities)
                    .Where(od => od.OrderCode == request.OrderCode)
                    .ToListAsync();

                using var transaction = await _dataContext.Database.BeginTransactionAsync();

                try
                {
                    // Hoàn kho
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
                                    BatchCode = $"RESTOCK-{request.OrderCode}-{DateTime.Now.Ticks}",
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

                    // Cập nhật trạng thái đơn hàng
                    order.Status = 6; // Đã hủy
                    _dataContext.Orders.Update(order);

                    // Cập nhật số lượng đã bán
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

                    return Ok(new
                    {
                        success = true,
                        message = "Order has been canceled successfully. Stock has been restored and refund request has been processed."
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Error during order cancellation: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CancelOrder error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 API XÁC NHẬN NHẬN HÀNG
        [HttpPost("ConfirmDelivery")]
        public async Task<IActionResult> ConfirmDelivery([FromBody] ConfirmDeliveryRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                var order = await _dataContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderCode == request.OrderCode && o.UserName == request.Email);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found or you do not have permission to confirm this delivery" });

                if (order.Status == 4) // Hàng đã giao tới nơi
                {
                    order.Status = 5; // Đơn hàng đã hoàn thành
                    await _dataContext.SaveChangesAsync();

                    return Ok(new { success = true, message = "Order has been confirmed as received" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "This order cannot be confirmed at this stage" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfirmDelivery error: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Helper methods
        private static string GetOrderStatusName(int status)
        {
            return status switch
            {
                1 => "New Order",
                2 => "Confirmed",
                3 => "In Transit",
                4 => "Delivered",
                5 => "Completed",
                6 => "Cancelled",
                _ => "Unknown"
            };
        }

        private static string GetPaymentMethod(string paymentIntentId)
        {
            if (string.IsNullOrEmpty(paymentIntentId) || paymentIntentId.StartsWith("COD"))
                return "Cash on Delivery";

            if (paymentIntentId.StartsWith("pi_") || paymentIntentId.StartsWith("cs_"))
                return "Stripe Card";

            if (paymentIntentId.StartsWith("PAYID-"))
                return "PayPal";

            return "Unknown";
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

    public class CancelOrderRequest
    {
        public string Email { get; set; }
        public string OrderCode { get; set; }
    }

    public class ConfirmDeliveryRequest
    {
        public string Email { get; set; }
        public string OrderCode { get; set; }
    }
}