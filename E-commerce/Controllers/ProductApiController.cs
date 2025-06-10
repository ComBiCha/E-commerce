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
                .Select(p => new ProductWithVariationsDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Image = p.Image,
                    Variations = p.Variations.Select(v => new VariationDto
                    {
                        Id = v.Id,
                        Size = v.Size,
                        Price = v.Price,
                        Stock = v.Stock,
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
