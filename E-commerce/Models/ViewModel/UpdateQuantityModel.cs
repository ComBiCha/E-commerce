namespace E_commerce.Models.ViewModel
{
    public class UpdateQuantityModel
    {
        public long ProductId { get; set; }
        public int VariationId { get; set; }
        public int NewQuantity { get; set; }
    }
}
