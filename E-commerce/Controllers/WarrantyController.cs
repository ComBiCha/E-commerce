using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Linq; 


public class WarrantyController : Controller
{
    private readonly DataContext _context;

    public WarrantyController(DataContext context)
    {
        _context = context;
    }

    public IActionResult RequestWarranty()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RequestWarranty(string warrantyCode, string reason)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Lấy WarrantyId từ WarrantyCode
        var warranty = await _context.Warranties.FirstOrDefaultAsync(w => w.WarrantyCode == warrantyCode);
        if (warranty == null)
        {
            TempData["error"] = "Invalid warranty code.";
            return View();
        }

        if (warranty.ExpirationDate < DateTime.UtcNow)
        {
            TempData["error"] = "The warranty has expired.";
            return View();
        }

        // Tạo yêu cầu bảo hành
        var request = new WarrantyRequestModel
        {
            WarrantyCode = warrantyCode,
            UserId = userId,
            Reason = reason,
            Status = 0, // Pending
            WarrantyID = warranty.Id // Gán WarrantyId từ bảng Warranties
        };

        // Thêm yêu cầu bảo hành vào cơ sở dữ liệu
        _context.Add(request);
        await _context.SaveChangesAsync();

        TempData["success"] = "Warranty request submitted successfully.";
        return RedirectToAction("Index", "Home");
    }
}
