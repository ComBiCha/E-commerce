using Microsoft.AspNetCore.Identity;

namespace E_commerce.Models
{
    public class AppUserModel : IdentityUser
    {
        public string Occupation { get; set; }
        public string RoleId { get; set; }
    }
}
