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

            string userA = currentUser;
            string userB = receiver;

            // Nếu người hiện tại là Admin, thì giả lập là CustomerSupport gửi
            if (role == "Admin")
            {
                userA = await _dataContext.Users
                    .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                    .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                    .Where(x => x.r.Name == "CustomerSupport")
                    .Select(x => x.u.UserName)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userA))
                    return Json(new List<object>());
            }

            // Trường hợp người dùng là User (khách), thì receiver chính là CustomerSupport
            // => Kiểm tra xem có hợp lệ không
            if (role == "User")
            {
                var supportUser = await _dataContext.Users
                    .Join(_dataContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                    .Join(_dataContext.Roles, uur => uur.ur.RoleId, r => r.Id, (uur, r) => new { uur.u, r })
                    .Where(x => x.r.Name == "CustomerSupport")
                    .Select(x => x.u.UserName)
                    .FirstOrDefaultAsync();

                if (supportUser == null)
                    return Json(new List<object>());

                userB = supportUser; // Receiver luôn là CustomerSupport nếu là khách
            }

            var messages = await _dataContext.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.Sender.UserName == userA && m.Receiver.UserName == userB) ||
                    (m.Sender.UserName == userB && m.Receiver.UserName == userA))
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    Sender = m.Sender.UserName,
                    Content = m.Content,
                    ImageUrl = m.ImageUrl
                })
                .ToListAsync();

            return Json(messages);
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

    }
}
