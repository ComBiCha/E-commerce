using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Polly;
using E_commerce.Areas.Admin.Repository;

namespace E_commerce.Controllers
{
    public class ChatController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly UserFacade _userFacade;


        public ChatController(DataContext context, UserFacade userFacade)
        {
            _dataContext = context;
            _userFacade = userFacade;
        }

        public IActionResult Index()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            var role = User.FindFirstValue(ClaimTypes.Role);
            ViewBag.Role = role;
            ViewBag.UserName = username;

            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetChatHistory(string receiver)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.Name);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(receiver))
                return BadRequest();

            // Lấy current user ID
            var currentUserId = await _dataContext.Users
                .Where(u => u.UserName == currentUser)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            // Lấy receiver user ID  
            var receiverUserId = await _dataContext.Users
                .Where(u => u.UserName == receiver)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(receiverUserId))
                return Json(new List<object>());

            string userAId = currentUserId;
            string userBId = receiverUserId;

            // Nếu người hiện tại là Admin, thì sử dụng CustomerSupport ID
            if (role == "Admin")
            {
                var customerSupportUserId = await _dataContext.Users
                    .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                    .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                    .Where(x => x.r.Name == "CustomerSupport")
                    .Select(x => x.u.Id)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(customerSupportUserId))
                    userAId = customerSupportUserId;
            }

            // Load messages
            var messages = await _dataContext.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.SenderId == userAId && m.ReceiverId == userBId) ||
                    (m.SenderId == userBId && m.ReceiverId == userAId))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // ===== MARK MESSAGES AS READ - QUAN TRỌNG =====
            // Mark tất cả tin nhắn từ receiver đến current user (hoặc CustomerSupport) là đã đọc
            var unreadMessages = messages
                .Where(m => m.ReceiverId == userAId && m.SenderId == userBId && !m.IsRead)
                .ToList();

            if (unreadMessages.Any())
            {
                Console.WriteLine($"🔵 Marking {unreadMessages.Count} messages as read from {receiver} to {currentUser}");
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }
                await _dataContext.SaveChangesAsync();
                Console.WriteLine($"✅ Messages marked as read successfully");
            }

            var result = messages.Select(m => new
            {
                Sender = m.Sender.UserName,
                Content = m.Content,
                ImageUrl = m.ImageUrl,
                timestamp = m.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss")
            }).ToList();

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No file uploaded");

            var fileName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + Path.GetExtension(image.FileName);
            var filePath = Path.Combine("wwwroot/media/chat", fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var imageUrl = Url.Content("~/media/chat/" + fileName);
            return Json(new { imageUrl });
        }

        [HttpGet]
        public async Task<IActionResult> GetChatUsers()
        {
            var currentUser = User.FindFirstValue(ClaimTypes.Name);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(role))
                return Unauthorized();

            string supportUserName = currentUser;

            if (role == "Admin")
            {
                // Tìm user có role CustomerSupport
                supportUserName = await _dataContext.Users
                    .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                    .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                    .Where(x => x.r.Name == "CustomerSupport")
                    .Select(x => x.u.UserName)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(supportUserName))
                    return Json(new List<string>());
            }

            if (role == "CustomerSupport" || role == "Admin")
            {
                var users = await _dataContext.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.Receiver.UserName == supportUserName)
                    .Select(m => m.Sender.UserName)
                    .Distinct()
                    .ToListAsync();

                return Json(users);
            }

            // Nếu là User => không trả danh sách gì cả
            return Json(new List<string>());
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerSupportUserName()
        {
            var supportUserName = await _userFacade.GetCustomerSupportUserNameAsync();
            Console.WriteLine($"CustomerSupport: {supportUserName}");
            return Json(supportUserName);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCounts()
        {
            try
            {
                var currentUser = User.FindFirstValue(ClaimTypes.Name);
                var role = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(role))
                {
                    return Unauthorized();
                }

                // Lấy current user ID
                var currentUserId = await _dataContext.Users
                    .Where(u => u.UserName == currentUser)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new Dictionary<string, int>());
                }

                // Nếu là Admin, lấy CustomerSupport user ID
                if (role == "Admin")
                {
                    var customerSupportUserId = await _dataContext.Users
                        .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                        .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                        .Where(x => x.r.Name == "CustomerSupport")
                        .Select(x => x.u.Id)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(customerSupportUserId))
                    {
                        currentUserId = customerSupportUserId;
                    }
                }

                // Đếm tin nhắn chưa đọc theo từng sender
                var unreadCounts = await _dataContext.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
                    .GroupBy(m => m.Sender.UserName)
                    .Select(g => new
                    {
                        SenderName = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var result = unreadCounts.ToDictionary(x => x.SenderName, x => x.Count);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new Dictionary<string, int>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkMessagesAsRead(string senderName)
        {
            try
            {
                var currentUser = User.FindFirstValue(ClaimTypes.Name);
                var role = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(senderName))
                {
                    return BadRequest("Invalid parameters");
                }

                // Lấy current user ID
                var currentUserId = await _dataContext.Users
                    .Where(u => u.UserName == currentUser)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();

                // Lấy sender user ID
                var senderUserId = await _dataContext.Users
                    .Where(u => u.UserName == senderName)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(senderUserId))
                {
                    return BadRequest("User not found");
                }

                string receiverUserId = currentUserId;

                // Nếu là Admin, dùng CustomerSupport ID
                if (role == "Admin")
                {
                    var customerSupportUserId = await _dataContext.Users
                        .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                        .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                        .Where(x => x.r.Name == "CustomerSupport")
                        .Select(x => x.u.Id)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(customerSupportUserId))
                        receiverUserId = customerSupportUserId;
                }

                // Mark tất cả tin nhắn từ sender đến receiver là đã đọc
                var unreadMessages = await _dataContext.Messages
                    .Where(m => m.SenderId == senderUserId && m.ReceiverId == receiverUserId && !m.IsRead)
                    .ToListAsync();

                if (unreadMessages.Any())
                {
                    Console.WriteLine($"🔵 Marking {unreadMessages.Count} messages from {senderName} as read");
                    foreach (var msg in unreadMessages)
                    {
                        msg.IsRead = true;
                    }
                    await _dataContext.SaveChangesAsync();
                    Console.WriteLine($"✅ All messages from {senderName} marked as read");
                }

                return Ok(new { success = true, markedCount = unreadMessages.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error marking messages as read: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> GetTotalUnreadCount()
        {
            try
            {
                var currentUser = User.FindFirstValue(ClaimTypes.Name);
                var role = User.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(role))
                {
                    return Unauthorized();
                }

                int totalUnread = 0;

                if (role == "User")
                {
                    // User: Đếm tin nhắn chưa đọc từ CustomerSupport gửi cho user này
                    var currentUserId = await _dataContext.Users
                        .Where(u => u.UserName == currentUser)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrEmpty(currentUserId))
                        return Json(0);

                    // Lấy CustomerSupport user ID
                    var customerSupportUserId = await _dataContext.Users
                        .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                        .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                        .Where(x => x.r.Name == "CustomerSupport")
                        .Select(x => x.u.Id)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(customerSupportUserId))
                    {
                        totalUnread = await _dataContext.Messages
                            .Where(m => m.ReceiverId == currentUserId && 
                                       m.SenderId == customerSupportUserId && 
                                       !m.IsRead)
                            .CountAsync();
                    }
                }
                else if (role == "Admin" || role == "CustomerSupport")
                {
                    // Admin/CustomerSupport: Đếm tổng tin nhắn chưa đọc từ tất cả users
                    string supportUserId = "";

                    if (role == "CustomerSupport")
                    {
                        // Nếu là CustomerSupport, dùng chính user hiện tại
                        supportUserId = await _dataContext.Users
                            .Where(u => u.UserName == currentUser)
                            .Select(u => u.Id)
                            .FirstOrDefaultAsync();
                    }
                    else if (role == "Admin")
                    {
                        // Nếu là Admin, lấy CustomerSupport user ID
                        supportUserId = await _dataContext.Users
                            .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                            .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                            .Where(x => x.r.Name == "CustomerSupport")
                            .Select(x => x.u.Id)
                            .FirstOrDefaultAsync();
                    }

                    if (!string.IsNullOrEmpty(supportUserId))
                    {
                        totalUnread = await _dataContext.Messages
                            .Where(m => m.ReceiverId == supportUserId && 
                                       !m.IsRead)
                            .CountAsync();
                    }
                }

                Console.WriteLine($"📊 GetTotalUnreadCount - User: {currentUser}, Role: {role}, Count: {totalUnread}");
                return Json(totalUnread);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetTotalUnreadCount: {ex.Message}");
                return Json(0);
            }
        }

    }
}
