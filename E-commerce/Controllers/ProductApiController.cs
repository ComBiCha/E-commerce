using E_commerce.Models;
using E_commerce.Models.DTOs;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_commerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductApiController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly BrandApiController _brandApiController;
        private readonly CategoryApiController _categoryApiController;

        public ProductApiController(
            DataContext context,
            BrandApiController brandApiController,
            CategoryApiController categoryApiController)
        {
            _context = context;
            _brandApiController = brandApiController;
            _categoryApiController = categoryApiController;
        }

        // 1. Lấy danh sách tất cả sản phẩm
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductWithVariationsDto>>> GetProducts()
        {
            var products = await _context.Products
                .Include(p => p.Variations)
                    .ThenInclude(v => v.Color)
                .Include(p => p.Variations)
                    .ThenInclude(v => v.ProductQuantities)
                .Select(p => new ProductWithVariationsDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Image = p.Image,
                    Image2 = p.Image2,
                    Variations = p.Variations.Select(v => new VariationDto
                    {
                        Id = v.Id,
                        Size = v.Size,
                        Price = v.Price,
                        Stock = v.ProductQuantities.Sum(q => q.Quantity), // Lấy tổng tồn kho
                        Image = v.ImageUrl,
                        Color = v.Color == null ? null : new ColorDto
                        {
                            Id = v.Color.Id,
                            Name = v.Color.Name,
                            HexCode = v.Color.HexCode
                        }
                    }).ToList()
                })
                .ToListAsync();

            return products;
        }


        [HttpGet("Search")]
    public async Task<IActionResult> Search(
        string? searchTerm,
        string? category,
        string? brand,
        string? sortOrder,
        [FromQuery] List<string>? colors,
        [FromQuery] List<string>? materials)
    {
        IQueryable<ProductModel> products = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variations)
                .ThenInclude(v => v.Color)
            .Include(p => p.Variations)
                .ThenInclude(v => v.Material);

        // Lọc theo từ khóa, danh mục, thương hiệu
        if (!string.IsNullOrEmpty(searchTerm))
        {
            products = products.Where(p => p.Name.Contains(searchTerm) || p.Category.Name.Contains(searchTerm));
        }
        if (!string.IsNullOrEmpty(category))
        {
            products = products.Where(p => p.Category.Name == category);
        }
        if (!string.IsNullOrEmpty(brand))
        {
            products = products.Where(p => p.Brand.Name == brand);
        }

        // Lọc theo màu sắc
        if (colors != null && colors.Count > 0)
        {
            products = products.Where(p => p.Variations.Any(v => colors.Contains(v.Color.Name)));
        }

        // Lọc theo chất liệu
        if (materials != null && materials.Count > 0)
        {
            products = products.Where(p => p.Variations.Any(v => materials.Contains(v.Material.Name)));
        }

        // Sắp xếp
        switch (sortOrder)
        {
            case "best_selling":
                products = products.OrderByDescending(p => p.Sold);
                break;
            case "price_desc":
                products = products.OrderByDescending(p => p.Price);
                break;
            case "price_asc":
                products = products.OrderBy(p => p.Price);
                break;
            default:
                products = products.OrderBy(p => p.Name);
                break;
        }

        var result = await products.Select(p => new {
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.Image,
            p.Image2,
            Category = p.Category.Name,
            Brand = p.Brand.Name,
            Variations = p.Variations.Select(v => new {
                v.Id,
                v.Size,
                v.Price,
                v.Stock,
                v.ImageUrl,
                Color = v.Color != null ? new { v.Color.Id, v.Color.Name, v.Color.HexCode } : null,
                Material = v.Material != null ? new { v.Material.Id, v.Material.Name } : null
            })
        }).ToListAsync();

        return Ok(result);
    }

        // 2. Lấy thông tin chi tiết sản phẩm theo Id
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductModel>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        // 3. Tạo sản phẩm mới
        [HttpPost]
        public async Task<ActionResult<ProductModel>> CreateProduct(ProductModel product)
        {
            // Kiểm tra Brand
            var brandResult = await _brandApiController.GetBrand(product.BrandId);
            if (brandResult.Result is NotFoundResult)
                return BadRequest("Invalid Brand ID");

            // Kiểm tra Category
            var categoryResult = await _categoryApiController.GetCategory(product.CategoryId);
            if (categoryResult.Result is NotFoundResult)
                return BadRequest("Invalid Category ID");

            // Lưu Product
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateProduct), new { id = product.Id }, product);
        }

        // 4. Cập nhật sản phẩm
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, ProductModel product)
        {
            if (id != product.Id)
            {
                return BadRequest("Product ID mismatch");
            }

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            // Cập nhật thông tin sản phẩm
            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.Description = product.Description;
            existingProduct.Image = product.Image;
            existingProduct.Quantity = product.Quantity;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.BrandId = product.BrandId;

            _context.Entry(existingProduct).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            return NoContent();
        }

        // 5. Xóa sản phẩm
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Hàm kiểm tra sản phẩm tồn tại
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
