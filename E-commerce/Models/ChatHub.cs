using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace E_commerce.Models
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly DataContext _dataContext;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(DataContext context, ILogger<ChatHub> logger)
        {
            _dataContext = context;
            _logger = logger;
        }

        public async Task SendMessage(string receiverName, string message, string imageUrl = null)
        {
            Console.WriteLine("=== SendMessage CALLED ===");
            Console.WriteLine($"ReceiverName: {receiverName}");
            Console.WriteLine($"Message: {message}");
            Console.WriteLine($"ImageUrl: {imageUrl}");
            
            try
            {
                var senderName = Context.User?.Identity?.Name;
                Console.WriteLine($"SenderName from Context: {senderName}");
                
                if (string.IsNullOrEmpty(senderName))
                {
                    Console.WriteLine("ERROR: Sender name is null or empty");
                    return;
                }

                var sender = _dataContext.Users.FirstOrDefault(u => u.UserName == senderName);
                var receiver = _dataContext.Users.FirstOrDefault(u => u.UserName == receiverName);

                Console.WriteLine($"Sender found: {sender?.UserName}");
                Console.WriteLine($"Receiver found: {receiver?.UserName}");

                if (sender == null || receiver == null)
                {
                    Console.WriteLine("ERROR: Sender or receiver not found");
                    return;
                }

                // Lưu message
                var msg = new Messages
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Content = message,
                    ImageUrl = imageUrl,
                    Timestamp = DateTime.Now
                };
                
                _dataContext.Messages.Add(msg);
                await _dataContext.SaveChangesAsync();
                Console.WriteLine("Message saved to database");

                // Gửi đến clients
                await Clients.User(receiver.Id).SendAsync("ReceiveMessage", senderName, message, imageUrl);
                await Clients.User(sender.Id).SendAsync("ReceiveMessage", senderName, message, imageUrl);
                Console.WriteLine("Message sent to clients");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
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
