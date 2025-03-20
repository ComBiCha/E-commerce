using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ProductService : IProductService
{
    private readonly DataContext _dataContext;

    public ProductService(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<List<ProductModel>> SearchProductsAsync(string searchTerm, string category, string brand)
    {
        var products = _dataContext.Products.Include(p => p.Category).Include(p => p.Brand).AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
            products = products.Where(p => p.Name.Contains(searchTerm) || p.Category.Name.Contains(searchTerm));

        if (!string.IsNullOrEmpty(category))
            products = products.Where(p => p.Category.Slug == category);

        if (!string.IsNullOrEmpty(brand))
            products = products.Where(p => p.Brand.Name == brand);

        return await products.ToListAsync();
    }

    public async Task<ProductModel> GetProductDetailsAsync(long id)
    {
        return await _dataContext.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Ratings)
            .Include(p => p.Variations)
                .ThenInclude(v => v.Material)
            .Include(p => p.Variations)
                .ThenInclude(v => v.Color)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
