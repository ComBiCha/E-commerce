using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public ProductController(DataContext context, IWebHostEnvironment webHostEnvironment)
        {
            _dataContext = context;
            _webHostEnvironment = webHostEnvironment;
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

			// Lấy danh sách sản phẩm kèm Variations
			List<ProductModel> products = await _dataContext.Products
				.Include(p => p.Category)
				.Include(p => p.Brand)
				.Include(p => p.Variations) // Include Variations để tính tổng Stock
				.ToListAsync();

			// Cập nhật Quantity = tổng Stock của tất cả Variations
			foreach (var product in products)
			{
				product.Quantity = product.Variations?.Sum(v => v.Stock) ?? 0;
			}

			int recsCount = products.Count();
			var pager = new Paginate(recsCount, pg, pageSize);
			int recSkip = (pg - 1) * pageSize;

			var data = products.Skip(recSkip).Take(pager.PageSize).ToList();

			ViewBag.Pager = pager;

			return View(data);
		}

		public async Task<IActionResult> Details(int id)
        {
            var product = await _dataContext.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Variations)
                .ThenInclude(v => v.Material)
                .Include(p => p.Variations)
                .ThenInclude(v => v.Color)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

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
            ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name");
            ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name");
            ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
            ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");
            if (ModelState.IsValid)
            {
                model.Slug = model.Name.Replace(" ", "-");
                // Upload ảnh chính cho sản phẩm
                if (model.ImageUpload != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageUpload.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageUpload.CopyToAsync(fileStream);
                    }

                    model.Image = uniqueFileName;
                }
                if (model.ImageUpload2 != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageUpload2.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageUpload2.CopyToAsync(fileStream);
                    }

                    model.Image2 = uniqueFileName;
                }
                if (model.Variations != null && model.Variations.Count > 0)
                {
                    model.Quantity = model.Variations.Sum(v => v.Stock);
                }
                _dataContext.Products.Add(model);
                await _dataContext.SaveChangesAsync(); // Lưu sản phẩm trước để có Id

                // Xử lý biến thể nếu có
                if (model.Variations != null && model.Variations.Count > 0)
                {
                    for (int i = 0; i < model.Variations.Count; i++)
                    {
                        var variationData = model.Variations[i];

                        // Kiểm tra nếu có ảnh thì mới xử lý
                        if (VariationImages != null && i < VariationImages.Count && VariationImages[i] != null)
                        {
                            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media/variations");
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + VariationImages[i].FileName;
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await VariationImages[i].CopyToAsync(fileStream);
                            }

                            // Chỉ tạo biến thể nếu có ảnh
                            var variation = new ProductVariationModel
                            {
                                MaterialId = variationData.MaterialId,
                                ColorId = variationData.ColorId,
                                Price = variationData.Price,
                                Stock = variationData.Stock,
                                Size = variationData.Size,
                                ProductId = model.Id,
                                ImageUrl = uniqueFileName
                            };

                            _dataContext.Variations.Add(variation);
                        }
                    }


                    var invalidVariations = _dataContext.Variations.Where(v => v.ImageUrl == null).ToList();
                    _dataContext.Variations.RemoveRange(invalidVariations);
                    model.Quantity = _dataContext.Variations.Where(v => v.ProductId == model.Id).Sum(v => v.Stock);
                    await _dataContext.SaveChangesAsync();

                }



                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
		public async Task<IActionResult> Edit(long Id)
		{
			var product = await _dataContext.Products
				.Include(p => p.Variations)
				.FirstOrDefaultAsync(p => p.Id == Id);

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
        public async Task<IActionResult> Edit(ProductModel product, List<IFormFile> VariationImages)
        {
            var existingProduct = await _dataContext.Products
                .Include(p => p.Variations)
                .FirstOrDefaultAsync(p => p.Id == product.Id);

            if (existingProduct == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name", product.CategoryId);
            ViewBag.Brands = new SelectList(_dataContext.Brands, "Id", "Name", product.BrandId);
            ViewBag.Materials = new SelectList(_dataContext.Materials, "Id", "Name");
            ViewBag.Colors = new SelectList(_dataContext.Colors, "Id", "Name");

            if (ModelState.IsValid)
            {
                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.BrandId = product.BrandId;
                existingProduct.WarrantyPeriod = product.WarrantyPeriod;

                if (product.ImageUpload != null)
                {
                    string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                    string newImageName = Guid.NewGuid().ToString() + "_" + product.ImageUpload.FileName;
                    string newFilePath = Path.Combine(uploadDir, newImageName);

                    if (!string.IsNullOrEmpty(existingProduct.Image))
                    {
                        string oldFilePath = Path.Combine(uploadDir, existingProduct.Image);
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    using (var fileStream = new FileStream(newFilePath, FileMode.Create))
                    {
                        await product.ImageUpload.CopyToAsync(fileStream);
                    }

                    existingProduct.Image = newImageName;
                }
				if (product.ImageUpload2 != null)
				{
					string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
					string newImageName = Guid.NewGuid().ToString() + "_" + product.ImageUpload2.FileName;
					string newFilePath = Path.Combine(uploadDir, newImageName);

					if (!string.IsNullOrEmpty(existingProduct.Image2))
					{
						string oldFilePath = Path.Combine(uploadDir, existingProduct.Image2);
						if (System.IO.File.Exists(oldFilePath))
						{
							System.IO.File.Delete(oldFilePath);
						}
					}

					using (var fileStream = new FileStream(newFilePath, FileMode.Create))
					{
						await product.ImageUpload2.CopyToAsync(fileStream);
					}

					existingProduct.Image2 = newImageName;
				}

				// Cập nhật các variations đã có
				for (int i = 0; i < existingProduct.Variations.Count; i++)
                {
                    var variation = existingProduct.Variations[i];
                    var updatedVariation = product.Variations[i];
                    variation.MaterialId = updatedVariation.MaterialId;
                    variation.ColorId = updatedVariation.ColorId;
                    variation.Price = updatedVariation.Price;
                    variation.Stock = updatedVariation.Stock;
					variation.Size = updatedVariation.Size;

					if (VariationImages != null && i < VariationImages.Count && VariationImages[i] != null)
                    {
                        string variationUploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/variations");
                        string newVariationImage = Guid.NewGuid().ToString() + "_" + VariationImages[i].FileName;
                        string newVariationPath = Path.Combine(variationUploadDir, newVariationImage);

                        if (!string.IsNullOrEmpty(variation.ImageUrl))
                        {
                            string oldVariationPath = Path.Combine(variationUploadDir, variation.ImageUrl);
                            if (System.IO.File.Exists(oldVariationPath))
                            {
                                System.IO.File.Delete(oldVariationPath);
                            }
                        }

                        using (var fileStream = new FileStream(newVariationPath, FileMode.Create))
                        {
                            await VariationImages[i].CopyToAsync(fileStream);
                        }

                        variation.ImageUrl = newVariationImage;
                    }
                }

                // Xử lý thêm variations mới
                if (product.Variations != null && product.Variations.Count > existingProduct.Variations.Count)
                {
                    for (int i = existingProduct.Variations.Count; i < product.Variations.Count; i++)
                    {
                        var newVariation = product.Variations[i];

                        string newVariationImage = null;
                        if (VariationImages != null && i < VariationImages.Count && VariationImages[i] != null)
                        {
                            string variationUploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/variations");
                            newVariationImage = Guid.NewGuid().ToString() + "_" + VariationImages[i].FileName;
                            string newVariationPath = Path.Combine(variationUploadDir, newVariationImage);

                            using (var fileStream = new FileStream(newVariationPath, FileMode.Create))
                            {
                                await VariationImages[i].CopyToAsync(fileStream);
                            }
                        }

                        var variation = new ProductVariationModel
                        {
                            ProductId = product.Id,
                            MaterialId = newVariation.MaterialId,
                            ColorId = newVariation.ColorId,
                            Price = newVariation.Price,
                            Stock = newVariation.Stock,
							Size = newVariation.Size,
							ImageUrl = newVariationImage
                        };

                        _dataContext.Variations.Add(variation);
                    }
                }

                existingProduct.Quantity = existingProduct.Variations.Sum(v => v.Stock);
                _dataContext.Update(existingProduct);
                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Product and variations updated successfully";
                return RedirectToAction("Index");
            }

            return View(product);
        }

        [HttpPost]
        public IActionResult DeleteVariation(int variationId, int productId)
        {
            var variation = _dataContext.Variations.FirstOrDefault(v => v.Id == variationId);
            if (variation == null)
            {
                return NotFound();
            }

            // Xóa variation
            _dataContext.Variations.Remove(variation);
            _dataContext.SaveChanges();

            return RedirectToAction("Details", new { id = productId });
        }


        [HttpPost]
        public IActionResult DeleteProduct(int productId)
        {
            var product = _dataContext.Products.FirstOrDefault(p => p.Id == productId);
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

            _dataContext.Products.Remove(product);
            _dataContext.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> AddQuantity(int Id)
        {
            var productbyquantity = await _dataContext.ProductQuantities.Where(pq => pq.ProductId == Id).ToListAsync();
            ViewBag.ProductByQuantity = productbyquantity;
            ViewBag.Id = Id;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StoreProductQuantity(ProductQuantityModel productQuantityModel)
        {
            var product = _dataContext.Products.Find(productQuantityModel.ProductId);
            if(product==null)
            {
                return NotFound();
            }
            product.Quantity += productQuantityModel.Quantity;

            productQuantityModel.Quantity = productQuantityModel.Quantity;
            productQuantityModel.ProductId = productQuantityModel.ProductId;
            productQuantityModel.DateCreated = DateTime.Now;

            _dataContext.Add(productQuantityModel);
            _dataContext.SaveChangesAsync();
            TempData["success"] = "Quantity added successfully";
            return RedirectToAction("AddQuantity", "Product", new { Id = productQuantityModel.ProductId });
        }
    }
}
