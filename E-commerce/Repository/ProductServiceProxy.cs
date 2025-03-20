using E_commerce.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ProductServiceProxy : IProductService
{
    private readonly ProductService _realService;
    private List<ProductModel> _cachedProducts;

    public ProductServiceProxy(ProductService realService)
    {
        _realService = realService;
        _cachedProducts = null;
    }

    public async Task<List<ProductModel>> SearchProductsAsync(string searchTerm, string category, string brand)
    {
        if (_cachedProducts == null)
        {
            System.Console.WriteLine("Loading products from database...");
            _cachedProducts = await _realService.SearchProductsAsync(searchTerm, category, brand);
        }
        else
        {
            System.Console.WriteLine("Returning cached products...");
        }

        return _cachedProducts;
    }

    public async Task<ProductModel> GetProductDetailsAsync(long id)
    {
        return await _realService.GetProductDetailsAsync(id);
    }
}
