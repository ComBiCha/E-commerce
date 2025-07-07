using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class ProductQuantityModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Initial quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Initial quantity cannot be negative")]
        public int InitialQuantity { get; set; } // Số lượng nhập ban đầu

        [Required(ErrorMessage = "Current quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Current quantity cannot be negative")]
        public int CurrentQuantityInBatch { get; set; } // Số còn lại

        [Required]
        public int VariationId { get; set; }

        [Required]
        public long BatchId { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime LastUpdated { get; set; } = DateTime.Now; // Thời gian cập nhật gần nhất

        [ForeignKey("VariationId")]
        public ProductVariationModel Variation { get; set; }

        [ForeignKey("BatchId")]
        public BatchModel Batch { get; set; }
    }
}
