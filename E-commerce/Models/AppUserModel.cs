using Microsoft.AspNetCore.Identity;

namespace E_commerce.Models
{
    public class AppUserModel : IdentityUser
    {
        public string Occupation { get; set; }
        public int Points { get; set; }
        public string RoleId { get; set; }
        public string Token { get; set; }
        public decimal GetDiscountRate()
        {
            if (Points >= 50000) return 0.08m; // 8%
            if (Points >= 40000) return 0.06m; // 6%
            if (Points >= 20000) return 0.04m; // 4%
            if (Points >= 10000) return 0.02m; // 2%
            return 0m; // Không giảm giá
        }
    }
}
