using E_commerce.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_commerce.Controllers
{
    public class ChatController : Controller
    {
        private readonly DataContext _dataContext;

        public ChatController(DataContext context)
        {
            _dataContext = context;
        }

        public IActionResult Index()
        {
            // Lấy username từ Claims
            var username = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserName = username;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            ViewBag.Username = username;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetChatHistory(string receiver)
        {
            // Lấy username từ Claims
            var currentUser = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(receiver))
                return BadRequest();

            var messages = await _dataContext.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.Sender.UserName == currentUser && m.Receiver.UserName == receiver) ||
                    (m.Sender.UserName == receiver && m.Receiver.UserName == currentUser))
                .OrderBy(m => m.Timestamp)
                .Select(m => new {
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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
