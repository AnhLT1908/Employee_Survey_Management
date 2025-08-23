using System;
using System.Linq;
using System.Threading.Tasks;
using HRTestInfrastructure.Data;
using HRTestInfrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly HRTestDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(
            HRTestDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ================== DANH SÁCH ==================
        public async Task<IActionResult> Index(string? q, int? departmentId, string? role, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            // base query: Users + Dept + Roles
            var baseQuery =
                from u in _db.Users
                join d in _db.Departments on u.DepartmentId equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                join ur in _db.UserRoles on u.Id equals ur.UserId into urj
                from ur in urj.DefaultIfEmpty()
                join r in _db.Roles on ur.RoleId equals r.Id into rj
                from r in rj.DefaultIfEmpty()
                select new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.NormalizedEmail,
                    DepartmentId = u.DepartmentId,
                    DeptName = d != null ? d.Name : null,
                    RoleName = r != null ? r.Name : null,
                    RoleNorm = r != null ? r.NormalizedName : null
                };

            // search
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpper();
                baseQuery = baseQuery.Where(x =>
                    (x.NormalizedEmail ?? "").Contains(k) ||
                    (x.FullName != null && x.FullName.ToUpper().Contains(k)));
            }

            // filter by department
            if (departmentId.HasValue)
                baseQuery = baseQuery.Where(x => x.DepartmentId == departmentId.Value);

            // filter by role
            if (!string.IsNullOrWhiteSpace(role) && role != "_all")
                baseQuery = baseQuery.Where(x => x.RoleNorm == role.ToUpper());

            // loại user có role Admin
            baseQuery = baseQuery.Where(x => x.RoleNorm == null || x.RoleNorm != "ADMIN");

            // distinct user ids
            var userIdsQuery = baseQuery.Select(x => x.Id).Distinct();
            var totalItems = await userIdsQuery.CountAsync();

            // get paged users
            var usersPage = await
                (from u in _db.Users
                 join d in _db.Departments on u.DepartmentId equals d.Id into dj
                 from d in dj.DefaultIfEmpty()
                 where userIdsQuery.Contains(u.Id)
                 select new { u.Id, u.FullName, u.Email, DeptName = d != null ? d.Name : null })
                .OrderBy(x => x.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pageIds = usersPage.Select(x => x.Id).ToList();

            // load roles for these users
            var rolesMap = await
                (from ur in _db.UserRoles
                 join r in _db.Roles on ur.RoleId equals r.Id
                 where pageIds.Contains(ur.UserId) && r.NormalizedName != "ADMIN"
                 select new { ur.UserId, r.Name })
                .ToListAsync();

            var vm = new UserIndexVM
            {
                Items = usersPage.Select(u => new UserListItemVM
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email!,
                    Department = u.DeptName,
                    Roles = string.Join(", ",
                        rolesMap.Where(r => r.UserId == u.Id)
                                .Select(r => r.Name)
                                .Distinct())
                }).ToList(),

                Q = q,
                DepartmentId = departmentId,
                Role = string.IsNullOrWhiteSpace(role) ? "_all" : role,

                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // lookups cho màn danh sách (KHÔNG chèn option "Tất cả..." tại đây)
            await FillLookups(vm);

            return View(vm);
        }

        // ================== DETAILS ==================
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var deptName = await _db.Departments
                .Where(d => d.Id == user.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin")) return Forbid();

            var vm = new UserDetailsVM
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Department = deptName,
                Roles = roles.ToList()
            };
            return View(vm);
        }

        // ================== CREATE ==================
        public async Task<IActionResult> Create()
        {
            var vm = new UserEditVM();
            await FillLookups(vm); // overload cho UserEditVM
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserEditVM vm)
        {
            await FillLookups(vm);
            if (!ModelState.IsValid) return View(vm);

            var user = new ApplicationUser
            {
                FullName = vm.FullName,
                Email = vm.Email,
                UserName = vm.Email,
                NormalizedEmail = vm.Email.ToUpper(),
                NormalizedUserName = vm.Email.ToUpper(),
                DepartmentId = vm.DepartmentId,
                EmailConfirmed = true
            };

            var createRes = await _userManager.CreateAsync(user, vm.Password!);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View(vm);
            }

            var targetRoles = vm.SelectedRoles ?? new();
            targetRoles.Remove("Admin");
            if (targetRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, targetRoles);

            TempData["Success"] = "Đã tạo người dùng.";
            return RedirectToAction(nameof(Index));
        }

        // ================== EDIT ==================
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin")) return Forbid();

            var vm = new UserEditVM
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                DepartmentId = user.DepartmentId,
                SelectedRoles = roles.Where(r => r != "Admin").ToList()
            };
            await FillLookups(vm); // overload cho UserEditVM
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditVM vm)
        {
            // Email field bị disabled -> không post, nhưng ta đã thêm hidden.
            // Để chắc chắn, bỏ Required trên Email khỏi ModelState khi edit:
            ModelState.Remove(nameof(UserEditVM.Email));

            await FillLookups(vm);
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == vm.Id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains("Admin")) return Forbid();

            // Cập nhật profile
            user.FullName = vm.FullName;
            user.DepartmentId = vm.DepartmentId;
            await _userManager.UpdateAsync(user);

            // Đồng bộ role (trừ Admin)
            var targetRoles = (vm.SelectedRoles ?? new()).Where(r => r != "Admin").ToList();
            var toAdd = targetRoles.Except(currentRoles).ToArray();
            var toRemove = currentRoles.Except(targetRoles).Where(r => r != "Admin").ToArray();
            if (toAdd.Length > 0) await _userManager.AddToRolesAsync(user, toAdd);
            if (toRemove.Length > 0) await _userManager.RemoveFromRolesAsync(user, toRemove);

            TempData["Success"] = "Đã cập nhật người dùng.";
            return RedirectToAction(nameof(Index));
        }

        // ================== DELETE ==================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin")) return Forbid();

            var deptName = await _db.Departments
                .Where(d => d.Id == user.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();

            var vm = new UserDetailsVM
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Department = deptName,
                Roles = roles.ToList()
            };
            return View(vm);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin")) return Forbid();

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                TempData["Error"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["Success"] = "Đã xoá người dùng.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ================== HELPERS ==================
        // Lookups cho màn DANH SÁCH
        private async Task FillLookups(UserIndexVM vm)
        {
            vm.Departments = await _db.Departments
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = vm.DepartmentId.HasValue && vm.DepartmentId.Value == x.Id
                })
                .ToListAsync();

            vm.Roles = await _roleManager.Roles
                .Where(r => r.NormalizedName != "ADMIN")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Name!,
                    Text = r.Name!,
                    Selected = !string.IsNullOrEmpty(vm.Role) &&
                               vm.Role.Equals(r.Name!, StringComparison.OrdinalIgnoreCase)
                })
                .ToListAsync();
        }

        // Lookups cho màn TẠO/SỬA
        private async Task FillLookups(UserEditVM vm)
        {
            vm.Departments = await _db.Departments
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = vm.DepartmentId.HasValue && vm.DepartmentId.Value == x.Id
                })
                .ToListAsync();

            vm.AllRoles = await _roleManager.Roles
                .Where(r => r.NormalizedName != "ADMIN")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Name!,
                    Text = r.Name!,
                    Selected = vm.SelectedRoles != null &&
                               vm.SelectedRoles.Contains(r.Name!)
                })
                .ToListAsync();
        }
    }

    // ================== ViewModels ==================
    public class UserListItemVM
    {
        public string Id { get; set; } = default!;
        public string? FullName { get; set; }
        public string Email { get; set; } = default!;
        public string? Department { get; set; }
        public string Roles { get; set; } = "";
    }

    public class UserDetailsVM
    {
        public string Id { get; set; } = default!;
        public string? FullName { get; set; }
        public string Email { get; set; } = default!;
        public string? Department { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class UserEditVM
    {
        public string? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "Họ tên")]
        public string? FullName { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = default!;

        [System.ComponentModel.DataAnnotations.Display(Name = "Phòng ban")]
        public int? DepartmentId { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string? Password { get; set; }

        [System.ComponentModel.DataAnnotations.Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Xác nhận mật khẩu")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string? ConfirmPassword { get; set; }

        public List<string>? SelectedRoles { get; set; } = new();
        public List<SelectListItem> AllRoles { get; set; } = new();
        public List<SelectListItem> Departments { get; set; } = new();
    }

    public class UserIndexVM
    {
        public List<UserListItemVM> Items { get; set; } = new();

        public string? Q { get; set; }
        public int? DepartmentId { get; set; }
        public string? Role { get; set; }

        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Roles { get; set; } = new();

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    }
}
