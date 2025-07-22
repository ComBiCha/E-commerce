using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]

    public class StatisticController : Controller
    {
        private readonly DataContext _context;

        public StatisticController(DataContext context)
        {
            _context = context;
        }

        // Action: Hiển thị trang thống kê
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Index2()
        {
            return View();
        }
        public IActionResult Revenue()
        {
            return View();
        }

        // Action: Lấy số lượng đơn hàng theo tháng
        public JsonResult GetMonthlyOrderCount()
        {
            try
            {
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }

                var orderCounts = orders
                    .Where(o => o.Status != 6)
                    .GroupBy(o => new { Year = o.CreatedDate.Year, Month = o.CreatedDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Month}/{g.Key.Year}",
                        OrderCount = g.Count()
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                return new JsonResult(orderCounts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMonthlyOrderCount: {ex.Message}\n{ex.StackTrace}");
                return new JsonResult(new { message = "An error occurred while processing your request." }) { StatusCode = 500 };
            }
        }

        public JsonResult GetMonthlyRevenue()
        {
            try
            {
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }
                var monthlyRevenue = orders
                    .Where(order => order.Status != 6)
                    .Join(_context.OrderDetails,
                          order => order.OrderCode,
                          detail => detail.OrderCode,
                          (order, detail) => new { order.CreatedDate, detail.Price, detail.DiscountAmount, detail.Quantity })
                    .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Month}/{g.Key.Year}",
                        Revenue = g.Sum(x => (x.Price * x.Quantity - x.DiscountAmount))
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                return new JsonResult(monthlyRevenue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMonthlyRevenue: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        public JsonResult GetQuarterlyRevenue()
        {
            try
            {
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }
                var quarterlyRevenue = orders
                    .Where(order => order.Status != 6)
                    .Join(_context.OrderDetails,
                          order => order.OrderCode,
                          detail => detail.OrderCode,
                          (order, detail) => new { order.CreatedDate, detail.Price, detail.DiscountAmount, detail.Quantity })
                    .GroupBy(o => new
                    {
                        o.CreatedDate.Year,
                        Quarter = (o.CreatedDate.Month - 1) / 3 + 1
                    })
                    .Select(g => new
                    {
                        Quarter = $"Q{g.Key.Quarter} {g.Key.Year}",
                        Revenue = g.Sum(x => (x.Price * x.Quantity - x.DiscountAmount))
                    })
                    .OrderBy(x => x.Quarter)
                    .ToList();

                return new JsonResult(quarterlyRevenue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetQuarterlyRevenue: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        public JsonResult GetTotalOrdersAndRevenue()
        {
            try
            {
                var orders = _context.Orders
                    .Where(o => o.Status != 6) // ❌ Bỏ đơn đã huỷ
                    .ToList();

                if (!orders.Any())
                {
                    return new JsonResult(new { message = "No valid orders found." });
                }

                var totalData = orders
                    .GroupJoin(
                        _context.OrderDetails,
                        order => order.OrderCode,
                        detail => detail.OrderCode,
                        (order, orderDetails) => new
                        {
                            Revenue = orderDetails.Sum(detail => (detail.Price * detail.Quantity - detail.DiscountAmount))
                        })
                    .ToList();

                var totalRevenue = totalData.Sum(x => x.Revenue);
                var totalOrders = orders.Count; // ✅ Sử dụng orders đã lọc

                return new JsonResult(new
                {
                    TotalOrders = totalOrders,
                    TotalRevenue = totalRevenue
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalOrdersAndRevenue: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        [HttpGet]
        public JsonResult GetTop5BestSeller()
        {
            try
            {
                var topProducts = _context.OrderDetails
                    .Join(_context.Orders,
                          od => od.OrderCode,
                          o => o.OrderCode,
                          (od, o) => new { OrderDetail = od, Order = o })
                    .Where(joined => joined.Order.Status != 6) // Loại trừ đơn bị hủy
                    .GroupBy(joined => joined.OrderDetail.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalQuantitySold = g.Sum(x => x.OrderDetail.Quantity)
                    })
                    .OrderByDescending(p => p.TotalQuantitySold)
                    .Take(5)
                    .Join(_context.Products,
                          od => od.ProductId,
                          p => p.Id,
                          (od, p) => new
                          {
                              p.Name,
                              p.Image,
                              od.TotalQuantitySold
                          })
                    .ToList();

                return new JsonResult(topProducts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTop5BestSellingProducts: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        public JsonResult GetProductStatistics()
        {
            try
            {
                var products = _context.Products;
                if (products == null || !products.Any())
                {
                    return new JsonResult(new { message = "No products found." });
                }
                // Thống kê tổng số lượng sản phẩm hiện có
                var totalQuantity = products.Count();

                // Thống kê số lượng sản phẩm theo loại
                var productsByCategory = products
                    .GroupBy(p => p.Category.Name)
                    .Select(g => new
                    {
                        Category = g.Key,
                        TotalProducts = g.Count(),
                    })
                    .OrderByDescending(x => x.TotalProducts)
                    .ToList();

                // Thống kê số lượng sản phẩm theo thương hiệu
                var productsByBrand = products
                    .GroupBy(p => p.Brand.Name)
                    .Select(g => new
                    {
                        Brand = g.Key,
                        TotalProducts = g.Count(),
                    })
                    .OrderByDescending(x => x.TotalProducts)
                    .ToList();

                // Kết quả trả về
                var result = new
                {
                    TotalQuantity = totalQuantity,
                    ProductsByCategory = productsByCategory,
                    ProductsByBrand = productsByBrand
                };

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetProductStatistics: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        public async Task<JsonResult> GetTotalUserCount()
        {
            // Đếm tổng số lượng người dùng hiện có
            var totalUserCount = await _context.Users.CountAsync();

            // Trả về kết quả dưới dạng Json
            return new JsonResult(totalUserCount);
        }
        // tính doanh thu trong ngày
        public JsonResult GetTodayRevenue()
        {
            try
            {
                // Lấy ngày hiện tại
                var today = DateTime.Today;

                // Lọc các đơn hàng trong ngày hiện tại
                var orders = _context.Orders
                    .Where(order => order.CreatedDate.Date == today)
                    .ToList();

                // Kiểm tra nếu không có đơn hàng
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found for today." });
                }

                // Tính doanh thu bằng cách group các đơn hàng với chi tiết của chúng
                var totalData = orders
                    .Where(order => order.Status != 6)
                    .GroupJoin(
                        _context.OrderDetails,
                        order => order.OrderCode,
                        detail => detail.OrderCode,
                        (order, orderDetails) => new
                        {
                            Revenue = orderDetails.Sum(detail => detail.Price * detail.Quantity - detail.DiscountAmount)
                        })
                    .ToList();

                // Tính tổng doanh thu
                var totalRevenue = totalData.Sum(x => x.Revenue);

                // Trả về kết quả
                return new JsonResult(new
                {
                    TotalRevenue = totalRevenue
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTodayRevenue: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        //Tính mức tăng trưởng so với tháng trước
        public JsonResult GetCurrentMonthRevenueGrowth()
        {
            try
            {
                // Lấy danh sách đơn hàng
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }

                // Tính tổng doanh thu từng tháng
                var monthlyRevenue = orders
                    .Where(order => order.Status != 6)
                    .Join(_context.OrderDetails,
                          order => order.OrderCode,
                          detail => detail.OrderCode,
                          (order, detail) => new { order.CreatedDate, detail.Price, detail.Quantity })
                    .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
                    .Select(g => new
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Revenue = g.Sum(x => x.Price * x.Quantity)
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                // Lấy tháng hiện tại và tháng trước
                var currentMonth = monthlyRevenue.LastOrDefault(); // Tháng hiện tại
                var previousMonth = monthlyRevenue.Count > 1 ? monthlyRevenue[^2] : null; // Tháng trước

                if (currentMonth == null || previousMonth == null)
                {
                    return new JsonResult(new { message = "Not enough data to calculate growth." });
                }

                // Tính mức tăng trưởng
                var growth = currentMonth.Revenue / previousMonth.Revenue * 100;

                // Trả về kết quả
                return new JsonResult(new
                {
                    CurrentMonth = currentMonth.Month.ToString("MM/yyyy"),
                    PreviousMonth = previousMonth.Month.ToString("MM/yyyy"),
                    Growth = growth
                });
            }
            catch (Exception ex)
            {
                // Log lỗi
                Console.WriteLine($"Error in GetCurrentMonthRevenueGrowth: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

        [HttpGet("GetRevenue")]
        public IActionResult GetRevenue(int? year, int? month, int? day)
        {
            var query = _context.Orders
                .Where(o => o.Status == 5)
                .Join(_context.OrderDetails,
                    o => o.OrderCode,
                    od => od.OrderCode,
                    (order, detail) => new
                    {
                        order.OrderCode,
                        order.CreatedDate,
                        Revenue = (detail.Price * detail.Quantity) - detail.DiscountAmount,
                        order.Status
                    });

            if (year.HasValue) query = query.Where(x => x.CreatedDate.Year == year.Value);
            if (month.HasValue) query = query.Where(x => x.CreatedDate.Month == month.Value);
            if (day.HasValue) query = query.Where(x => x.CreatedDate.Day == day.Value);

            var result = query.GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month, o.CreatedDate.Day, o.OrderCode })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    orderCode = g.Key.OrderCode,  // Chỉnh về chữ thường để khớp với JSON
                    createdDate = g.First().CreatedDate,
                    revenue = g.Sum(x => x.Revenue),
                    status = g.First().Status
                })
                .OrderByDescending(x => x.createdDate)
                .ToList();
            return Ok(result);
        }

        [HttpGet("GetRevenue2")]
        public IActionResult GetRevenue2(int? year, int? month, int? day)
        {
            var query = _context.Orders
                .Where(o => o.Status == 5)  // Chỉ lấy đơn hàng đã hoàn thành
                .Join(_context.OrderDetails,
                    o => o.OrderCode,
                    od => od.OrderCode,
                    (order, detail) => new
                    {
                        order.CreatedDate,
                        Revenue = (detail.Price * detail.Quantity) - (detail.DiscountAmount)  // Tránh null
                    });

            if (year.HasValue) query = query.Where(x => x.CreatedDate.Year == year.Value);
            if (month.HasValue) query = query.Where(x => x.CreatedDate.Month == month.Value);
            if (day.HasValue) query = query.Where(x => x.CreatedDate.Day == day.Value);

            decimal totalRevenue = query.Any() ? query.Sum(x => x.Revenue) : 0;  // Tránh lỗi nếu không có dữ liệu

            return Ok(new { totalRevenue });
        }



        // Thống kê doanh thu theo khoảng thời gian
        [HttpGet("revenue-range")]
        public IActionResult GetRevenueRange(DateTime startDate, DateTime endDate)
        {
            var revenue = _context.Orders
                .Where(o => o.Status == 5 && o.CreatedDate >= startDate && o.CreatedDate <= endDate)
                .Join(_context.OrderDetails,
                    o => o.OrderCode,
                    od => od.OrderCode,
                    (order, detail) => new
                    {
                        order.CreatedDate,
                        Revenue = (detail.Price * detail.Quantity) - detail.DiscountAmount
                    })
                .GroupBy(o => o.CreatedDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Revenue)
                })
                .OrderBy(x => x.Date)
                .ToList();

            return Ok(revenue);
        }
        
        [HttpGet]
        public async Task<IActionResult> GetRevenueByBrand(int month, int year)
        {
        var data = await (
        from od in _context.OrderDetails
        join p in _context.Products on od.ProductId equals p.Id
        join b in _context.Brands on p.BrandId equals b.Id
        join o in _context.Orders on od.OrderCode equals o.OrderCode
        where o.CreatedDate.Month == month &&
        o.CreatedDate.Year == year &&
        o.Status == 5
        group new { od, p, b } by b.Name into g
        select new
        {
        brand = g.Key,
        revenue = g.Sum(x => (x.od.Price - x.od.DiscountAmount) * x.od.Quantity)
        }
        ).ToListAsync();

        return Json(data);
        }
	}
}
