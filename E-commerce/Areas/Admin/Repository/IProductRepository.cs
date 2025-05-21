using E_commerce.Models;
using E_commerce.Areas.Admin.Repository;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers.RepositoryPattern
{
	public interface IProductRepository
	{
		Task<IEnumerable<ProductModel>> GetAllProductsAsync();
		Task<ProductModel> GetProductByIdAsync(long id);
		Task<ProductModel> GetProductBySlugAsync(string slug);
		Task CreateProductAsync(ProductModel product);
		Task UpdateProductAsync(ProductModel product);
		Task DeleteProductAsync(ProductModel product);
	}
}

