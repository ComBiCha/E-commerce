using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class WishlistApiController : ControllerBase
{
    private readonly DataContext _datacontext;

    public WishlistApiController(DataContext datacontext)
    {
        _datacontext = datacontext;
    }

    [HttpGet("GetWishlist")]
    public async Task<IActionResult> GetWishlist([FromQuery] string userId)
    {
        try
        {
            Console.WriteLine($"📡 Getting wishlist for userId: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"📡 No userId provided - returning null to preserve local data");
                return Ok(null);
            }

            var wishlistItems = await (from w in _datacontext.Wishlists
                                       join p in _datacontext.Products on w.ProductId equals p.Id
                                       where w.UserId == userId
                                       select new
                                       {
                                           Id = w.Id,
                                           ProductId = p.Id,
                                           ProductName = p.Name,
                                           Price = p.Price,
                                           Image = p.Image, // ✅ Sửa từ ImageUrl thành Image
                                           Image2 = p.Image2, // ✅ Thêm Image2 
                                           Description = p.Description,
                                           // ✅ Bỏ Images và CreatedAt vì không có
                                           BrandName = p.Brand.Name,
                                           CategoryName = p.Category.Name,
                                           Variations = p.Variations
                                       }).ToListAsync();

            Console.WriteLine($"📡 Found {wishlistItems.Count} wishlist items for user {userId}");
            return Ok(wishlistItems);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Error getting wishlist: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("AddToWishlist")]
    public async Task<IActionResult> AddToWishlist([FromBody] WishlistRequest request) // ✅ Đổi tên class
    {
        try
        {
            Console.WriteLine($"📡 Adding productId {request.ProductId} to wishlist for user {request.UserId}");

            // Check if already exists
            var existingWishlist = await _datacontext.Wishlists
                .FirstOrDefaultAsync(w => w.ProductId == request.ProductId && w.UserId == request.UserId);

            if (existingWishlist != null)
            {
                Console.WriteLine($"⚠️ Product {request.ProductId} already in wishlist for user {request.UserId}");
                return Ok(new { success = true, message = "Product already in wishlist" });
            }

            // Add new wishlist item
            var newWishlist = new WishlistModel
            {
                ProductId = request.ProductId,
                UserId = request.UserId
                // ✅ Bỏ CreatedAt vì không có trong model
            };

            _datacontext.Wishlists.Add(newWishlist);
            await _datacontext.SaveChangesAsync();

            Console.WriteLine($"✅ Added productId {request.ProductId} to wishlist for user {request.UserId}");
            return Ok(new { success = true, message = "Added to wishlist" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Error adding to wishlist: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpDelete("RemoveFromWishlist")]
    public async Task<IActionResult> RemoveFromWishlist([FromQuery] string userId, [FromQuery] long productId) // ✅ Đổi int thành long
    {
        try
        {
            Console.WriteLine($"📡 Removing productId {productId} from wishlist for user {userId}");

            var wishlist = await _datacontext.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

            if (wishlist == null)
            {
                Console.WriteLine($"⚠️ Product {productId} not found in wishlist for user {userId}");
                return Ok(new { success = true, message = "Product not in wishlist" });
            }

            _datacontext.Wishlists.Remove(wishlist);
            await _datacontext.SaveChangesAsync();

            Console.WriteLine($"✅ Removed productId {productId} from wishlist for user {userId}");
            return Ok(new { success = true, message = "Removed from wishlist" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Error removing from wishlist: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }
}

// ✅ DTO class với tên mới
public class WishlistRequest
{
    public long ProductId { get; set; } // ✅ Đổi int thành long để match ProductModel
    public string UserId { get; set; }
}