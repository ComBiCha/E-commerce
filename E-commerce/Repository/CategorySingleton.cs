using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace E_commerce.Services
{
    public class CategorySingleton
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public CategorySingleton(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        // Create Category
        public async Task<bool> CreateCategoryAsync(CategoryModel category)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                // Giữ nguyên chữ viết hoa/thường trong Slug
                category.Slug = category.Name.Replace(" ", "-");

                if (await dataContext.Categories.AnyAsync(c => c.Slug == category.Slug))
                {
                    return false; // Category already exists
                }

                dataContext.Categories.Add(category);
                await dataContext.SaveChangesAsync();
                return true;
            }
        }


        // Edit Category
        public async Task<bool> EditCategoryAsync(int id, CategoryModel updatedCategory)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var category = await dataContext.Categories.FindAsync(id);
                if (category == null)
                {
                    return false; // Category not found
                }

                // Tạo Slug từ Name
                updatedCategory.Slug = updatedCategory.Name.Replace(" ", "-");

                if (await dataContext.Categories.AnyAsync(c => c.Slug == updatedCategory.Slug && c.Id != id))
                {
                    return false; // Slug already exists for another category
                }

                // Cập nhật các trường
                category.Name = updatedCategory.Name;
                category.Slug = updatedCategory.Slug;
                category.Description = updatedCategory.Description; // Cập nhật Description

                dataContext.Categories.Update(category);
                await dataContext.SaveChangesAsync();
                return true;
            }
        }



        // Delete Category
        public async Task<bool> DeleteCategoryAsync(int id)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var category = await dataContext.Categories.FindAsync(id);
                if (category == null)
                {
                    return false; // Category not found
                }

                dataContext.Categories.Remove(category);
                await dataContext.SaveChangesAsync();
                return true;
            }
        }

        public async Task<CategoryModel> GetCategoryByIdAsync(int id)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                return await dataContext.Categories.FindAsync(id);
            }
        }

        public static CategorySingleton _instance;
        private static readonly object _lock = new object();

        public static CategorySingleton GetInstance(IServiceScopeFactory scopeFactory)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CategorySingleton(scopeFactory);
                    }
                }
            }
            return _instance;
        }


        public async Task<List<CategoryModel>> GetAllCategoriesAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                return await dataContext.Categories.ToListAsync();
            }
        }

    }
}
