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

        // Action: Lấy số lượng đơn hàng theo tháng
        public JsonResult GetMonthlyOrderCount()
        {
            try
            {
                // Ensure Orders are properly retrieved
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }

                // Group orders by Year and Month
                var orderCounts = orders
                    .GroupBy(o => new { Year = o.CreatedDate.Year, Month = o.CreatedDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Month}/{g.Key.Year}",
                        OrderCount = g.Count()
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                // Check if grouping resulted in no data
                if (orderCounts == null || !orderCounts.Any())
                {
                    return new JsonResult(new { message = "No data available." });
                }

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
                    .Join(_context.OrderDetails,
                          order => order.OrderCode,
                          detail => detail.OrderCode,
                          (order, detail) => new { order.CreatedDate, detail.Price, detail.Quantity })
                    .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Month}/{g.Key.Year}",
                        Revenue = g.Sum(x => x.Price * x.Quantity)
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                return new JsonResult(monthlyRevenue);
            }
            catch (Exception ex)
            {
                // Log the exception with more context
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
                    .Join(_context.OrderDetails,
                          order => order.OrderCode,
                          detail => detail.OrderCode,
                          (order, detail) => new { order.CreatedDate, detail.Price, detail.Quantity })
                    .GroupBy(o => new
                    {
                        o.CreatedDate.Year,
                        Quarter = (o.CreatedDate.Month - 1) / 3 + 1
                    })
                    .Select(g => new
                    {
                        Quarter = $"Q{g.Key.Quarter} {g.Key.Year}",
                        Revenue = g.Sum(x => x.Price * x.Quantity)
                    })
                    .OrderBy(x => x.Quarter)
                    .ToList();

                return new JsonResult(quarterlyRevenue);
            }
            catch (Exception ex)
            {
                // Log the exception with more context
                Console.WriteLine($"Error in GetQuarterlyRevenue: {ex.Message}");
                return new JsonResult(null) { StatusCode = 500 };
            }
        }

		public JsonResult GetTotalOrdersAndRevenue()
		{
			try
			{
                var orders = _context.Orders.ToList();
                if (orders == null || !orders.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }
                var totalData = orders
					.GroupJoin(
						_context.OrderDetails,
						order => order.OrderCode,
						detail => detail.OrderCode,
						(order, orderDetails) => new
						{
							Revenue = orderDetails.Sum(detail => detail.Price * detail.Quantity)
						})
					.ToList();

				var totalRevenue = totalData.Sum(x => x.Revenue);
				var totalOrders = totalData.Count();

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
                var orderDetail = _context.OrderDetails.ToList();
                if (orderDetail == null || !orderDetail.Any())
                {
                    return new JsonResult(new { message = "No orders found." });
                }
                var topProducts = orderDetail
                    .GroupBy(od => od.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalQuantitySold = g.Sum(od => od.Quantity)
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
                    .ToList();

                // Thống kê số lượng sản phẩm theo thương hiệu
                var productsByBrand = products
                    .GroupBy(p => p.Brand.Name)
                    .Select(g => new
                    {
                        Brand = g.Key,
                        TotalProducts = g.Count(),
                    })
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
    }
}
