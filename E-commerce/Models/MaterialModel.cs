namespace E_commerce.Models
{
    public class MaterialModel
    {
        public int Id { get; set; }
        public string Name { get; set; } // Da, Vải, Nhựa...
        public List<ProductVariationModel> Variations { get; set; }
    }
}
