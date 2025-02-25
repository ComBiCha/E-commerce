using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class ProductQuantityModel
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Quantity is required")]
        public int Quantity { get; set; }
        public int VariationId { get; set; }
        public DateTime DateCreated { get; set; }
        [ForeignKey("VariationId")]
        public ProductVariationModel Variation { get; set; }
    }
}
