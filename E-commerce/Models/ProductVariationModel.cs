using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class ProductVariationModel
    {
        public int Id { get; set; }
        public long ProductId { get; set; }  // Khóa ngoại đến Product
        public ProductModel Product { get; set; }

        public int MaterialId { get; set; }  // Chất liệu
        public MaterialModel Material { get; set; }

        public int ColorId { get; set; }  // Màu sắc
        public ColorModel Color { get; set; }

        public decimal Price { get; set; }  // Giá riêng cho biến thể (nếu có)
        public int Size { get; set; }


        public string ImageUrl { get; set; } // Ảnh biến thể
        [NotMapped]
        public IFormFile ImageUpload { get; set; } // Hỗ trợ upload file
        public ICollection<ProductQuantityModel> ProductQuantities { get; set; } = new List<ProductQuantityModel>();

        // Tính toán số lượng tồn kho dựa trên ProductQuantities
        [NotMapped]
        public int Stock => ProductQuantities?.Sum(q => q.CurrentQuantityInBatch) ?? 0;
    }
}