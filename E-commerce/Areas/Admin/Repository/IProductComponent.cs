namespace E_commerce.Areas.Admin.Repository
{
    public interface IProductComponent
    {
        int GetStock();
    }
    public class ProductVariation : IProductComponent
    {
        public int stock { get; set; }
        public string imageUrl { get; set; }
        public int GetStock()
        {
            return stock;
        }
    }
    public class ProductComposite : IProductComponent
    {
        private readonly List<IProductComponent> components = new List<IProductComponent>();
        public void AddComponent(IProductComponent component)
        {
            components.Add(component);
        }
        public void RemoveComponent(IProductComponent component)
        {
            components.Remove(component);
        }
        public void UpadateComponet(ProductComposite oldComponent, IProductComponent newComponent)
        {
            var index = components.IndexOf(oldComponent);
            if (index != -1)
            {
                components[index] = newComponent;
            }
        }
        public int GetStock()
        {
            return components.Sum(c => c.GetStock());
        }
        public List<IProductComponent> GetComponents()
        {
            return components;
        }
    }
}
