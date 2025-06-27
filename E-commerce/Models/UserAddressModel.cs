using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class UserAddressModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required]
        [MaxLength(15)]
        public string PhoneNumber { get; set; }

        [Required]
        [MaxLength(100)]
        public string City { get; set; }

        [Required]
        [MaxLength(100)]
        public string District { get; set; }

        [Required]
        [MaxLength(100)]
        public string Ward { get; set; }

        [Required]
        [MaxLength(200)]
        public string DetailAddress { get; set; }

        public bool IsDefault { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("UserId")]
        public virtual AppUserModel User { get; set; }

        public string GetFullAddress()
        {
            return $"{DetailAddress}, {Ward}, {District}, {City}";
        }
    }
}