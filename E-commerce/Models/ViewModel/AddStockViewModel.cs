using System.ComponentModel.DataAnnotations;
namespace E_commerce.Models.ViewModel
{
    public class AddStockViewModel
    {
        public ProductVariationModel Variation { get; set; }

        [Required]
        public string BatchCode { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        // Tổng tồn kho của sản phẩm
        public int TotalProductStock { get; set; }

        // Danh sách tất cả biến thể của sản phẩm để hiển thị
        public List<ProductVariationModel> AllVariations { get; set; } = new();
    }

}
