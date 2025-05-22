namespace E_commerce.Models
{
    public class ColorModel
    {
        public int Id { get; set; }
        public string Name { get; set; } // Tên màu (Đỏ, Đen, Xanh...)
        public string HexCode { get; set; } // Mã màu (#000000, #FF0000...)
        public List<ProductVariationModel> Variations { get; set; }
    }
}
