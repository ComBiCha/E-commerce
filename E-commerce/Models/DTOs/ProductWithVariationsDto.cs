namespace E_commerce.Models.DTOs
{
    public class ProductWithVariationsDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Image { get; set; }
        public string? Image2 { get; set; }
        public List<VariationDto> Variations { get; set; } = new();
    }

    public class VariationDto
    {
        public int Id { get; set; }
        public int Size { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public ColorDto? Color { get; set; }
        public string? Image { get;set; }
    }

    public class ColorDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? HexCode { get; set; }
    }
}
