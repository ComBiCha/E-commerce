using E_commerce.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace E_commerce.Services
{
    public class RoleManagerDecorator
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<AppUserModel> _userManager;

        public RoleManagerDecorator(RoleManager<IdentityRole> roleManager, UserManager<AppUserModel> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // Lấy danh sách tất cả Role
        public async Task<List<IdentityRole>> GetAllRolesAsync()
        {
            return new List<IdentityRole>(await _roleManager.Roles.ToListAsync());
        }

        // Kiểm tra Role có tồn tại không
        public async Task<bool> RoleExistsAsync(string roleName)
        {
            return await _roleManager.RoleExistsAsync(roleName);
        }

        // Thêm Role với kiểm tra ràng buộc
        public async Task<IdentityResult> CreateRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return IdentityResult.Failed(new IdentityError { Description = "Tên Role không hợp lệ!" });
            }

            if (await RoleExistsAsync(roleName))
            {
                return IdentityResult.Failed(new IdentityError { Description = "Role đã tồn tại!" });
            }

            return await _roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // Tìm Role theo ID
        public async Task<IdentityRole?> FindByIdAsync(string id)
        {
            return await _roleManager.FindByIdAsync(id);
        }

        // Sửa Role với kiểm tra ràng buộc
        public async Task<IdentityResult> UpdateRoleAsync(IdentityRole role)
        {
            var existingRole = await _roleManager.FindByIdAsync(role.Id);
            if (existingRole == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Role không tồn tại!" });
            }

            // Kiểm tra Role Name đã tồn tại chưa
            var roleWithSameName = await _roleManager.FindByNameAsync(role.Name);
            if (roleWithSameName != null && roleWithSameName.Id != role.Id)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Role Name đã tồn tại!" });
            }

            return await _roleManager.UpdateAsync(role);
        }

        // Xóa Role với kiểm tra ràng buộc
        public async Task<IdentityResult> DeleteRoleAsync(IdentityRole role)
        {
            if (role == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Role không tồn tại!" });
            }

            // Kiểm tra xem có User nào thuộc Role này không
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Count > 0)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Không thể xóa Role vì vẫn có người dùng đang sử dụng!" });
            }

            return await _roleManager.DeleteAsync(role);
        }
    }
}
