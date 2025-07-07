using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace E_commerce.Models
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly DataContext _dataContext;
        private readonly ILogger<ChatHub> _logger;
        private readonly UserManager<AppUserModel> _userManager;

        public ChatHub(DataContext context, ILogger<ChatHub> logger, UserManager<AppUserModel> userManager)
        {
            _dataContext = context;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task SendMessage(string receiverName, string message, string imageUrl = null)
        {
            Console.WriteLine("=== SendMessage CALLED ===");
            Console.WriteLine($"Message: {message}");
            Console.WriteLine($"ImageUrl: {imageUrl}");

            try
            {
                var actualSenderName = Context.User?.Identity?.Name;
                if (string.IsNullOrEmpty(actualSenderName))
                {
                    Console.WriteLine("ERROR: Sender name is null or empty");
                    return;
                }

                var actualSender = await _userManager.FindByNameAsync(actualSenderName);
                AppUserModel senderForMessage; // Người gửi thực sự của tin nhắn (có thể là CS nếu Admin gửi)

                // Xác định người gửi tin nhắn (senderForMessage)
                if (await _userManager.IsInRoleAsync(actualSender, "Admin"))
                {
                    senderForMessage = await (from u in _dataContext.Users
                                              join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                              join r in _dataContext.Roles on ur.RoleId equals r.Id
                                              where r.Name == "CustomerSupport"
                                              select u).FirstOrDefaultAsync();
                    if (senderForMessage == null)
                    {
                        Console.WriteLine("ERROR: CustomerSupport user not found.");
                        return;
                    }
                }
                else
                {
                    senderForMessage = actualSender;
                }

                receiverName = receiverName?.Trim('"');
                var receiver = await _dataContext.Users.FirstOrDefaultAsync(u => u.UserName == receiverName);

                if (senderForMessage == null || receiver == null)
                {
                    Console.WriteLine("ERROR: Sender or receiver not found");
                    return;
                }

                // Kiểm tra nếu là cuộc trò chuyện mới (giữa senderForMessage và receiver)
                bool isNewChat = !_dataContext.Messages.Any(m =>
                    (m.SenderId == senderForMessage.Id && m.ReceiverId == receiver.Id) ||
                    (m.SenderId == receiver.Id && m.ReceiverId == senderForMessage.Id));

                // Lưu tin nhắn
                var msg = new Messages
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = senderForMessage.Id,
                    ReceiverId = receiver.Id,
                    Content = message,
                    ImageUrl = imageUrl,
                    Timestamp = DateTime.Now
                };

                _dataContext.Messages.Add(msg);
                await _dataContext.SaveChangesAsync();


                await Clients.User(receiver.Id).SendAsync("ReceiveMessage", senderForMessage.UserName, message, imageUrl);

                await Clients.User(senderForMessage.Id).SendAsync("ReceiveMessage", senderForMessage.UserName, message, imageUrl);

                if (actualSender.Id != senderForMessage.Id) 
                {
                    await Clients.User(actualSender.Id).SendAsync("ReceiveMessage", senderForMessage.UserName, message, imageUrl);
                }
                if (await _userManager.IsInRoleAsync(actualSender, "User") || await _userManager.IsInRoleAsync(actualSender, "CustomerSupport"))
                {
                    var supportAndAdminIds = await (from u in _dataContext.Users
                                                    join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                                    join r in _dataContext.Roles on ur.RoleId equals r.Id
                                                    where r.Name == "CustomerSupport" || r.Name == "Admin"
                                                    select u.Id).Distinct().ToListAsync();

                    foreach (var id in supportAndAdminIds)
                    {
                        if (id != actualSender.Id && id != receiver.Id)
                        {
                            await Clients.User(id).SendAsync("ReceiveMessage", senderForMessage.UserName, message, imageUrl);
                        }
                    }
                }

                if (isNewChat)
                {
                    var supportAndAdminIds = await (from u in _dataContext.Users
                                                    join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                                    join r in _dataContext.Roles on ur.RoleId equals r.Id
                                                    where r.Name == "CustomerSupport" || r.Name == "Admin"
                                                    select u.Id).Distinct().ToListAsync();

                    foreach (var id in supportAndAdminIds)
                    {
                        await Clients.User(id).SendAsync("UpdateUserList", actualSender.UserName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in SendMessage: {Message}", ex.Message);
                throw;
            }
        }

        public async Task UpdateUserListClient()
        {
            var currentUser = Context.User?.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser)) return;

            var user = await _userManager.FindByNameAsync(currentUser);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("CustomerSupport") || roles.Contains("Admin"))
            {
                await Clients.Caller.SendAsync("LoadUserList");
            }
        }


        public override async Task OnConnectedAsync()
        {
            var username = Context.User?.Identity?.Name;
            _logger.LogInformation($"User connected: {username}");
            if (!string.IsNullOrEmpty(username))
            {
                Context.Items["UserName"] = username;
            }
            await base.OnConnectedAsync();
        }
    }
}