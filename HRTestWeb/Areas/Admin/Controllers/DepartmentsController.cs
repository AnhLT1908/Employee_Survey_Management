using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using HRTestInfrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DepartmentsController : Controller
    {
        private readonly HRTestDbContext _db;

        public DepartmentsController(HRTestDbContext db)
        {
            _db = db;
        }

        // ============ INDEX ============
        // /Admin/Departments
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            var baseQuery = _db.Departments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                baseQuery = baseQuery.Where(x =>
                    x.Name.Contains(k) || (x.Description != null && x.Description.Contains(k)));
            }

            var totalItems = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(x => x.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new DepartmentListItemVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    MemberCount = _db.Users.Count(u => u.DepartmentId == x.Id)
                })
                .ToListAsync();

            var vm = new DepartmentIndexVM
            {
                Items = items,
                Q = q,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(vm);
        }

        // ============ DETAILS ============
        // /Admin/Departments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var d = await _db.Departments
                .Select(x => new DepartmentDetailsVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    MemberCount = _db.Users.Count(u => u.DepartmentId == x.Id)
                })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null)
            {
                TempData["Error"] = "Phòng ban không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            return View(d);
        }

        // ============ CREATE ============
        // GET
        public IActionResult Create() => View(new DepartmentEditVM());

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentEditVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var entity = new HRTestDomain.Entities.Department
            {
                Name = vm.Name!.Trim(),
                Description = vm.Description
            };
            _db.Departments.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã tạo phòng ban.";
            return RedirectToAction(nameof(Index));
        }

        // ============ EDIT ============
        // GET
        public async Task<IActionResult> Edit(int id)
        {
            var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null)
            {
                TempData["Error"] = "Phòng ban không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new DepartmentEditVM
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description
            };
            return View(vm);
        }

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DepartmentEditVM vm)
        {
            if (id != vm.Id) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null)
            {
                TempData["Error"] = "Phòng ban không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            d.Name = vm.Name!.Trim();
            d.Description = vm.Description;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật phòng ban.";
            return RedirectToAction(nameof(Index));
        }

        // ============ DELETE ============
        // GET xác nhận xoá
        public async Task<IActionResult> Delete(int id)
        {
            var d = await _db.Departments
                .Select(x => new DepartmentDetailsVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    MemberCount = _db.Users.Count(u => u.DepartmentId == x.Id)
                })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null)
            {
                TempData["Error"] = "Phòng ban không tồn tại hoặc đã bị xoá.";
                return RedirectToAction(nameof(Index));
            }
            return View(d);
        }

        // POST xoá (ActionName = "Delete" để khớp với view)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null)
            {
                TempData["Error"] = "Phòng ban không tồn tại hoặc đã bị xoá.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _db.Departments.Remove(d);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Đã xoá phòng ban.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Không thể xoá vì đang có nhân viên thuộc phòng ban này.";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    // ============== VIEW MODELS ==============

    public class DepartmentListItemVM
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }

    public class DepartmentIndexVM
    {
        public List<DepartmentListItemVM> Items { get; set; } = new();
        public string? Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    }

    public class DepartmentEditVM
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "Tên phòng ban")]
        public string? Name { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }

    public class DepartmentDetailsVM
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }
}
