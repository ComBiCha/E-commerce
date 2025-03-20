using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Areas.Admin.Repository
{
    public class UserFacade
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;

        public UserFacade(UserManager<AppUserModel> userManager, RoleManager<IdentityRole> roleManager, DataContext dataContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _dataContext = dataContext;
        }

        public async Task<List<AppUserModel>> GetAllUsersAsync()
        {
            return await _dataContext.Users.ToListAsync();
        }

        public async Task<List<dynamic>> GetUsersWithRolesAsync()
        {
            var usersWithRoles = await (from u in _dataContext.Users
                                        join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                        join r in _dataContext.Roles on ur.RoleId equals r.Id
                                        select new
                                        {
                                            User = u,
                                            RoleName = r.Name
                                        }).ToListAsync();

            // Chuyển danh sách sang kiểu dynamic
            var result = usersWithRoles.Select(x => (dynamic)x).ToList();

            // Trả về kết quả
            return result;
        }


        public async Task<IdentityResult> CreateUserAsync(AppUserModel user, string roleId)
        {
            var createUserResult = await _userManager.CreateAsync(user, user.PasswordHash);
            if (createUserResult.Succeeded)
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role != null)
                {
                    var addToRoleResult = await _userManager.AddToRoleAsync(user, role.Name);
                    if (!addToRoleResult.Succeeded)
                    {
                        return IdentityResult.Failed(addToRoleResult.Errors.ToArray());
                    }
                }
            }
            return createUserResult;
        }

        public async Task<IdentityResult> DeleteUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                return await _userManager.DeleteAsync(user);
            }
            return IdentityResult.Failed(new IdentityError { Description = "User not found" });
        }

        public async Task<AppUserModel> FindUserByIdAsync(string id)
        {
            return await _userManager.FindByIdAsync(id);
        }

        // Lấy thông tin user theo ID
        public async Task<AppUserModel> GetUserByIdAsync(string id)
        {
            return await _userManager.FindByIdAsync(id);
        }

        // Lấy danh sách các roles
        public async Task<List<IdentityRole>> GetRolesAsync()
        {
            return await _roleManager.Roles.ToListAsync();
        }

        // Cập nhật thông tin user và role
        public async Task<IdentityResult> UpdateUserWithRoleAsync(string id, AppUserModel updatedUser)
        {
            var existingUser = await _userManager.FindByIdAsync(id);
            if (existingUser == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            // Cập nhật thông tin user
            existingUser.UserName = updatedUser.UserName;
            existingUser.Email = updatedUser.Email;
            existingUser.PhoneNumber = updatedUser.PhoneNumber;

            // Xử lý vai trò
            var oldRoles = await _userManager.GetRolesAsync(existingUser);
            var newRole = await _roleManager.FindByIdAsync(updatedUser.RoleId);
            if (newRole == null)
            {
                throw new InvalidOperationException("Role not found.");
            }

            // Xóa role cũ và thêm role mới
            await _userManager.RemoveFromRolesAsync(existingUser, oldRoles);
            await _userManager.AddToRoleAsync(existingUser, newRole.Name);

            // Cập nhật user
            return await _userManager.UpdateAsync(existingUser);
        }

        public async Task<List<IdentityRole>> GetAllRolesAsync()
        {
            return await _roleManager.Roles.ToListAsync();
        }
    }

}
