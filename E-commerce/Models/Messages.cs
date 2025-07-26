using E_commerce.Models;

namespace E_commerce.Models
{
    public class Messages
    {
        public string Id { get; set; }
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string? Content { get; set; }
        public string? ImageUrl { get; set; } 
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; } = false;

        public AppUserModel Sender { get; set; }
        public AppUserModel Receiver { get; set; }

    }
}
