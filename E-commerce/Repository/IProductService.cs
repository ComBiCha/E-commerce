using E_commerce.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IProductService
{
    Task<List<ProductModel>> SearchProductsAsync(string searchTerm, string category, string brand);
    Task<ProductModel> GetProductDetailsAsync(long id);
}
