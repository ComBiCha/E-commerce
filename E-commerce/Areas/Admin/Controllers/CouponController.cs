using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = "Admin")]
	public class CouponController : Controller
	{
		private readonly DataContext _dataContext;
		private readonly IWebHostEnvironment _webHostEnvironment;
		public CouponController(DataContext context, IWebHostEnvironment webHostEnvironment)
		{
			_dataContext = context;
			_webHostEnvironment = webHostEnvironment;
		}
        public async Task<IActionResult> Index(int pg = 1)
        {
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            // Đếm tổng số records
            int recsCount = await _dataContext.Coupons.CountAsync();
            var pager = new Paginate(recsCount, pg, pageSize);

            // Lấy data với Skip/Take trực tiếp từ DB
            int recSkip = (pg - 1) * pageSize;
            var coupons = await _dataContext.Coupons
                .OrderBy(c => c.Id) // Sắp xếp theo ID tăng dần
                .Skip(recSkip)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Pager = pager;
            return View(coupons);
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CouponModel coupon)
        {

            if (ModelState.IsValid)
            {
                _dataContext.Add(coupon);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Coupon added successfully";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["error"] = "Model error";
                List<string> errors = new List<string>();
                foreach (var value in ModelState.Values)
                {
                    foreach (var error in value.Errors)
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }
                string errorMessage = string.Join("\n", errors);
                return BadRequest(errorMessage);
            }
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int Id)
        {
            CouponModel coupon = await _dataContext.Coupons.FindAsync(Id);
            if (coupon == null)
            {
                return NotFound();
            }
            return View(coupon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int Id, CouponModel coupon)
        {
            if (Id != coupon.Id)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var exist_coupon = await _dataContext.Coupons.FindAsync(Id);
                    if (exist_coupon == null)
                    {
                        return NotFound();
                    }

                    // Cập nhật các properties theo CouponModel
                    exist_coupon.Code = coupon.Code;
                    exist_coupon.DiscountAmount = coupon.DiscountAmount;
                    exist_coupon.IsPercentage = coupon.IsPercentage;
                    exist_coupon.MinOrderValue = coupon.MinOrderValue;
                    exist_coupon.MaxUsage = coupon.MaxUsage;
                    exist_coupon.UsedCount = coupon.UsedCount;
                    exist_coupon.ExpiryDate = coupon.ExpiryDate;

                    _dataContext.Update(exist_coupon);
                    await _dataContext.SaveChangesAsync();
                    TempData["success"] = "Coupon updated successfully";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "An error occurred while updating the coupon");
                    return View(coupon);
                }
            }
            else
            {
                TempData["error"] = "Model validation failed";
                return View(coupon);
            }
        }
    }
}
