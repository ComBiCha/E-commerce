using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models
{
    public class BatchModel
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string BatchCode { get; set; }

        [Required]
        public DateTime ImportDate { get; set; } = DateTime.Now;


        public ICollection<ProductQuantityModel> ProductQuantities { get; set; }
    }
}
