namespace E_commerce.Models
{
    using Microsoft.AspNetCore.SignalR;

    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
    }

}
