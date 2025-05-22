using E_commerce.Models;
using E_commerce.Areas.Admin.Repository;
using Microsoft.EntityFrameworkCore;
using E_commerce.Repository;

namespace E_commerce.Areas.Admin.Controllers.RepositoryPattern
{
	public class ProductRepository : IProductRepository
	{
		private readonly DataContext _dataContext;
		public ProductRepository(DataContext context)
		{
			_dataContext = context;
		}
		public async Task<IEnumerable<ProductModel>> GetAllProductsAsync()
		{
			return await _dataContext.Products
				.ToListAsync();
		}
		public async Task<ProductModel> GetProductByIdAsync(long id)
		{
			return await _dataContext.Products
			.Include(p => p.Variations)
			.FirstOrDefaultAsync(p => p.Id == id);
		}
		public async Task<ProductModel> GetProductBySlugAsync(string slug)
		{
			return await _dataContext.Products
				.FirstOrDefaultAsync(p => p.Slug == slug);
		}
		public async Task CreateProductAsync(ProductModel product)
		{
			await _dataContext.Products.AddAsync(product);
			await _dataContext.SaveChangesAsync();
		}
		public async Task UpdateProductAsync(ProductModel product)
		{
			_dataContext.Products.Update(product);
			await _dataContext.SaveChangesAsync();
		}
		public async Task DeleteProductAsync(ProductModel product)
		{
			_dataContext.Products.Remove(product);
			await _dataContext.SaveChangesAsync();
		}
	}
}
