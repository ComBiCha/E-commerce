using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WarrantyController : Controller
    {
        private readonly DataContext _context;

        public WarrantyController(DataContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.WarrantyRequests
                .Include(r => r.Warranty)
                .Include(r => r.User).OrderByDescending(o => o.CreatedDate)
                .ToListAsync();
            return View(requests);
        }

        public async Task<IActionResult> ViewDetails(int id)
        {
            var request = await _context.WarrantyRequests
                .Include(r => r.Warranty)
                .Include(r => r.User)
                .Include(r => r.Warranty.Product)
                .Include(r => r.Warranty.Variation)
                .Include(r => r.Warranty.Variation.Material)
                .Include(r => r.Warranty.Variation.Color)
                .FirstOrDefaultAsync(r => r.Id == id);

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == request.Warranty.OrderCode);
            var userEmail = order.UserName;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            decimal discountRate = user?.GetDiscountRate() ?? 0m; // Lấy mức giảm giá từ UserModel
            decimal productTotal = request.Warranty.Product.Price;

            decimal discountAmount = productTotal * discountRate; // Số tiền giảm giá

            ViewBag.Order = order;
            ViewBag.DiscountRate = discountRate; // Gửi Discount Rate sang View
            ViewBag.DiscountAmount = discountAmount; // Số tiền giảm giá
            ViewBag.ProductTotal = productTotal;

            if (request == null)
            {
                TempData["error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(request); // Pass the warranty request to the view
        }

        // Update status to the next step (Accept)
        [HttpPost]
        public async Task<IActionResult> Accept(int id)
        {
            var warrantyRequest = _context.WarrantyRequests.FirstOrDefault(w => w.Id == id);
            if (warrantyRequest == null)
            {
                return Json(new { success = false, message = "Warranty request not found." });
            }

            warrantyRequest.Status = 1; // Approved
            warrantyRequest.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, newStatus = warrantyRequest.Status });
        }

        // Action để chuyển trạng thái sang Processing
        [HttpPost]
        public async Task<IActionResult> SetProcessing(int id)
        {
            var warrantyRequest = _context.WarrantyRequests.FirstOrDefault(w => w.Id == id);
            if (warrantyRequest == null)
            {
                return Json(new { success = false, message = "Warranty request not found." });
            }

            warrantyRequest.Status = 2; // Processing
            warrantyRequest.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, newStatus = warrantyRequest.Status });
        }

        // Action để chuyển trạng thái sang Under Inspection
        [HttpPost]
        public async Task<IActionResult> SetUnderInspection(int id)
        {
            var warrantyRequest = _context.WarrantyRequests.FirstOrDefault(w => w.Id == id);
            if (warrantyRequest == null)
            {
                return Json(new { success = false, message = "Warranty request not found." });
            }

            warrantyRequest.Status = 3; // Under Inspection
            warrantyRequest.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, newStatus = warrantyRequest.Status });
        }

        // Action để hoàn thành yêu cầu bảo hành
        [HttpPost]
        public async Task<IActionResult> SetComplete(int id)
        {
            var warrantyRequest = _context.WarrantyRequests.FirstOrDefault(w => w.Id == id);
            if (warrantyRequest == null)
            {
                return Json(new { success = false, message = "Warranty request not found." });
            }

            warrantyRequest.Status = 4; // Completed
            warrantyRequest.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, newStatus = warrantyRequest.Status });
        }

        // Action để deny yêu cầu bảo hành
        [HttpPost]
        public async Task<IActionResult> Deny(int id)
        {
            var warrantyRequest = _context.WarrantyRequests.FirstOrDefault(w => w.Id == id);
            if (warrantyRequest == null)
            {
                return Json(new { success = false, message = "Warranty request not found." });
            }

            warrantyRequest.Status = 5; // Denied
            warrantyRequest.UpdatedDate = DateTime.Now;

            _context.SaveChanges();

            return Json(new { success = true, newStatus = warrantyRequest.Status });
        }
    }
}
