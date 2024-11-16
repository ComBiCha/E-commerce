using Microsoft.AspNetCore.Mvc;
using E_commerce.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using E_commerce.Repository;

namespace E_commerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandApiController : ControllerBase
    {
        private readonly DataContext _context;

        public BrandApiController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BrandModel>>> GetBrands()
        {
            return await _context.Brands.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BrandModel>> GetBrand(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
                return NotFound();

            return brand;
        }

        [HttpPost]
        public async Task<ActionResult<BrandModel>> CreateBrand(BrandModel brand)
        {
            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
        }
    }
}
