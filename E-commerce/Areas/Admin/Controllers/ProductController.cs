using E_commerce.Areas.Admin.Controllers.RepositoryPattern;
using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Models.ViewModel;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace E_commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
	[Authorize(Roles = "Admin")]
	public class ProductController : Controller
	{
		private readonly DataContext _dataContext;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly IProductRepository _productRepository;
		public ProductController(DataContext context, IWebHostEnvironment webHostEnvironment, IProductRepository productRepository)
		{
			_dataContext = context;
			_webHostEnvironment = webHostEnvironment;
			_productRepository = productRepository;
		}
        /*public async Task<IActionResult> Index()
        {
            return View(await _dataContext.Products.OrderByDescending(p => p.Id).Include(p => p.Category).Include(p => p.Brand).ToListAsync());
        }*/
        public async Task<IActionResult> Index(int pg = 1)
        {
            const int pageSize = 10;

            if (pg < 1)
                pg = 1;

            // Lấy danh sách sản phẩm cùng biến thể và tồn kho
            List<ProductModel> products = await _dataContext.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Variations)
                    .ThenInclude(v => v.ProductQuantities)
                .ToListAsync();

			// Lấy tổng số sản phẩm đã bán từ bảng OrderDetails
			var soldMap = await _dataContext.OrderDetails
	            .Where(od => _dataContext.Orders
		            .Where(o => o.Status != 6)
		            .Select(o => o.OrderCode)
		            .Contains(od.OrderCode))
	            .GroupBy(od => od.ProductId)
	            .Select(g => new { ProductId = g.Key, SoldQty = g.Sum(od => od.Quantity) })
	            .ToDictionaryAsync(x => x.ProductId, x => x.SoldQty);

			// Gán số lượng đã bán vào từng sản phẩm
			foreach (var product in products)
			{
				product.Sold = soldMap.ContainsKey(product.Id) ? soldMap[product.Id] : 0;
			}

			// Phân trang
			int recsCount = products.Count();
            var pager = new Paginate(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;

            var data = products.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }


        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var product = await _dataContext.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Variations)
                    .ThenInclude(v => v.Color)
                .Include(p => p.Variations)
                    .ThenInclude(v => v.Material)
                .Include(p => p.Variations)
                    .ThenInclude(v => v.ProductQuantities)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            // Tính toán tổng tồn kho
            var totalStock = product.Variations.Sum(v => v.Stock);

            // Gửi xuống View
            ViewBag.TotalStock = totalStock;

            return View(product);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name");
            ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name");
            ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
            ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductModel model, List<IFormFile> VariationImages)
        {
            // Gán lại dropdown
            ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name");
            ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name");
            ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
            ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");

            // Tạo slug từ tên sản phẩm
            model.Slug = model.Name?.Replace(" ", "-");

            // Kiểm tra hợp lệ
            if (!ModelState.IsValid || await _productRepository.GetProductBySlugAsync(model.Slug) != null)
            {
                ModelState.AddModelError("", "Product already exists or input invalid.");
                return View(model);
            }

            // Tách biến thể ra khỏi model
            var variationsToAdd = model.Variations?.ToList() ?? new List<ProductVariationModel>();
            model.Variations = null;

            // Upload ảnh sản phẩm chính
            if (model.ImageUpload != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                string uniqueFileName = Guid.NewGuid() + "_" + model.ImageUpload.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageUpload.CopyToAsync(stream);
                }
                model.Image = uniqueFileName;
            }

            // Upload ảnh phụ
            if (model.ImageUpload2 != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                string uniqueFileName = Guid.NewGuid() + "_" + model.ImageUpload2.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageUpload2.CopyToAsync(stream);
                }
                model.Image2 = uniqueFileName;
            }

            // Lưu sản phẩm
            await _productRepository.CreateProductAsync(model);
            await _dataContext.SaveChangesAsync(); // để lấy được model.Id

            // Thêm các biến thể
            int imageIndex = 0;
            foreach (var variation in variationsToAdd)
            {
                string variationImageName = null;

                if (VariationImages != null && imageIndex < VariationImages.Count && VariationImages[imageIndex] != null)
                {
                    var imageFile = VariationImages[imageIndex];
                    string variationFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/variations");
                    Directory.CreateDirectory(variationFolder); // Đảm bảo folder tồn tại

                    variationImageName = Guid.NewGuid() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(variationFolder, variationImageName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                }

                _dataContext.Variations.Add(new ProductVariationModel
                {
                    ProductId = model.Id,
                    MaterialId = variation.MaterialId,
                    ColorId = variation.ColorId,
                    Price = variation.Price,
                    Size = variation.Size,
                    ImageUrl = variationImageName
                });

                imageIndex++;
            }

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Product added successfully";
            return RedirectToAction("Index");
        }





        public async Task<IActionResult> Edit(long Id)
		{
			var product = await _productRepository.GetProductByIdAsync(Id);

			if (product == null)
			{
				return NotFound();
			}

			ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name", product.CategoryId);
			ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name", product.BrandId);
			ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
			ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");
			ViewBag.OldImage = product.Image;
			ViewBag.OldImage2 = product.Image2;

			return View(product);
		}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductModel model)
        {
            ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name", model.CategoryId);
            ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name", model.BrandId);
            ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
            ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");

            var product = await _productRepository.GetProductByIdAsync(model.Id);
            if (product == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.CategoryId = model.CategoryId;
            product.BrandId = model.BrandId;
            product.WarrantyPeriod = model.WarrantyPeriod;
            product.Slug = model.Name?.Replace(" ", "-");

            // Ảnh chính
            if (model.ImageUpload != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                string uniqueFileName = Guid.NewGuid() + "_" + model.ImageUpload.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageUpload.CopyToAsync(stream);
                }
                product.Image = uniqueFileName;
            }

            // Ảnh phụ
            if (model.ImageUpload2 != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                string uniqueFileName = Guid.NewGuid() + "_" + model.ImageUpload2.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageUpload2.CopyToAsync(stream);
                }
                product.Image2 = uniqueFileName;
            }

            // Cập nhật
            await _productRepository.UpdateProductAsync(product);
            await _dataContext.SaveChangesAsync();

            // Cập nhật/Thêm biến thể
            foreach (var variation in model.Variations)
            {
                ProductVariationModel entity;
                if (variation.Id != 0)
                {
                    entity = await _dataContext.Variations.FindAsync(variation.Id);
                    if (entity == null) continue;
                }
                else
                {
                    entity = new ProductVariationModel { ProductId = product.Id };
                    _dataContext.Variations.Add(entity);
                }

                entity.MaterialId = variation.MaterialId;
                entity.ColorId = variation.ColorId;
                entity.Price = variation.Price;
                entity.Size = variation.Size;

                if (variation.ImageUpload != null && variation.ImageUpload.Length > 0)
                {
                    string variationFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/variations");
                    string variationImageName = Guid.NewGuid() + "_" + variation.ImageUpload.FileName;
                    string filePath = Path.Combine(variationFolder, variationImageName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await variation.ImageUpload.CopyToAsync(stream);
                    }
                    entity.ImageUrl = variationImageName;
                }
            }

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Product updated successfully.";
            return RedirectToAction("Index");
        }



        [HttpPost]
		public IActionResult DeleteVariation(int variationId, int productId)
		{
			try
			{
				var product = _dataContext.Products.Include(p => p.Variations).FirstOrDefault(p => p.Id == productId);
				if (product == null)
				{
					TempData["ErrorMessage"] = "Sản phẩm không tồn tại!";
					return RedirectToAction("Index");
				}

				var variation = _dataContext.Variations.FirstOrDefault(v => v.Id == variationId);
				if (variation == null)
				{
					TempData["ErrorMessage"] = "Variation không tồn tại!";
					return RedirectToAction("Edit", new { id = productId });
				}

				var productComposite = new ProductComposite();
				foreach (var v in product.Variations)
				{
					productComposite.AddComponent(new ProductVariation
					{
						stock = v.Stock,
						imageUrl = v.ImageUrl
					});
				}

				var relatedQuantities = _dataContext.ProductQuantities.Where(q => q.VariationId == variationId).ToList();
				if (relatedQuantities.Any())
				{
					_dataContext.ProductQuantities.RemoveRange(relatedQuantities);
				}

				//Xóa trong Composite
				var variationToDelete = productComposite.GetComponents().OfType<ProductVariation>().FirstOrDefault(v => v.stock == variation.Stock);
				if (variationToDelete != null)
				{
					productComposite.RemoveComponent(variationToDelete);
				}


				//Xóa trong database
				_dataContext.Variations.Remove(variation);
				_dataContext.SaveChanges();

				TempData["SuccessMessage"] = "Xóa Variation thành công!";
			}
			catch (DbUpdateException ex)
			{
				if (ex.InnerException is SqlException sqlEx)
				{
					if (sqlEx.Message.Contains("FK_OrderDetails_Variations"))
					{
						TempData["ErrorMessage"] = "Không thể xóa Variation vì có đơn hàng đang sử dụng nó!";
					}
					else if (sqlEx.Message.Contains("FK_Warranties_Variations"))
					{
						TempData["ErrorMessage"] = "Không thể xóa Variation vì đang có bảo hành liên kết!";
					}
					else
					{
						TempData["ErrorMessage"] = "Lỗi khi xóa Variation: " + ex.Message;
					}
				}
				else
				{
					TempData["ErrorMessage"] = "Lỗi không xác định khi xóa Variation!";
				}
			}
			return RedirectToAction("Details", new { id = productId });
		}

		[HttpPost]
		public async Task<IActionResult> DeleteProduct(int productId)
		{
			var product = await _productRepository.GetProductByIdAsync(productId);
			if (product == null)
			{
				return NotFound();
			}

			// Kiểm tra nếu sản phẩm không còn variations mới được phép xoá
			bool hasRemainingVariations = _dataContext.Variations.Any(v => v.ProductId == productId);
			if (hasRemainingVariations)
			{
				return BadRequest("Cannot delete the product because it still has variations.");
			}
			// Xóa ảnh sản phẩm
			if (!string.IsNullOrEmpty(product.Image))
			{
				string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
				string oldfilePath = Path.Combine(uploadDir, product.Image);
				if (System.IO.File.Exists(oldfilePath))
				{
					System.IO.File.Delete(oldfilePath);
				}
			}

			await _productRepository.DeleteProductAsync(product);
			TempData["success"] = "Product deleted successfully";
			_dataContext.SaveChanges();
			return RedirectToAction("Index");
		}

        [HttpGet]
        public async Task<IActionResult> AddQuantity(int variationId)
        {
            var variation = await _dataContext.Variations
                .Include(v => v.Product)
                .Include(v => v.Color)
                .Include(v => v.Material)
                .FirstOrDefaultAsync(v => v.Id == variationId);

            if (variation == null)
                return NotFound();

            var vm = new AddStockViewModel
            {
                Variation = variation
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StoreProductQuantity(AddStockViewModel vm)
        {
            var variation = await _dataContext.Variations
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == vm.Variation.Id);

            if (!ModelState.IsValid || variation == null)
            {
                vm.Variation = variation;
                return View("AddQuantity", vm);
            }

            // Check or create batch
            var existingBatch = await _dataContext.Batches
                .FirstOrDefaultAsync(b => b.BatchCode == vm.BatchCode);

            if (existingBatch == null)
            {
                existingBatch = new BatchModel
                {
                    BatchCode = vm.BatchCode,
                    ImportDate = DateTime.Now
                };
                _dataContext.Batches.Add(existingBatch);
                await _dataContext.SaveChangesAsync();
            }

            var pq = new ProductQuantityModel
            {
                VariationId = variation.Id,
                BatchId = existingBatch.Id,
                InitialQuantity = vm.Quantity,
                CurrentQuantityInBatch = vm.Quantity,
                DateCreated = DateTime.Now,
                LastUpdated = DateTime.Now
            };

            _dataContext.ProductQuantities.Add(pq);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Quantity added successfully!";
            return RedirectToAction("Details", "Product", new { id = variation.ProductId });
        }

    }
}
