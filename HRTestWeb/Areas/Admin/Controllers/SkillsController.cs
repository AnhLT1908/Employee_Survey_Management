using System;
using System.Linq;
using System.Threading.Tasks;
using HRTestDomain.Entities;
using HRTestInfrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SkillsController : Controller
    {
        private readonly HRTestDbContext _db;
        private const int PAGE_SIZE = 10;

        public SkillsController(HRTestDbContext db)
        {
            _db = db;
        }

        // GET: /Admin/Skills
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            if (page < 1) page = 1;

            var baseQuery = _db.Skills.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpper();
                baseQuery = baseQuery.Where(s =>
                    s.Name.ToUpper().Contains(k) ||
                    (s.Description != null && s.Description.ToUpper().Contains(k)));
            }

            var totalItems = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(s => s.Name)
                .Skip((page - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .AsNoTracking()
                .Select(s => new SkillListItemVM
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description
                })
                .ToListAsync();

            var vm = new SkillIndexVM
            {
                Items = items,
                Q = q,
                Page = page,
                PageSize = PAGE_SIZE,
                TotalItems = totalItems
            };

            return View(vm);
        }

        // GET: /Admin/Skills/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var s = await _db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            return View(new SkillDetailsVM
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            });
        }

        // GET: /Admin/Skills/Create
        public IActionResult Create() => View(new SkillEditVM());

        // POST: /Admin/Skills/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SkillEditVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var exists = await _db.Skills.AnyAsync(x => x.Name.ToUpper() == vm.Name!.Trim().ToUpper());
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Name), "Tên kỹ năng đã tồn tại.");
                return View(vm);
            }

            var entity = new Skill { Name = vm.Name!.Trim(), Description = vm.Description };
            _db.Skills.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã tạo kỹ năng.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Skills/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            return View(new SkillEditVM
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            });
        }

        // POST: /Admin/Skills/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SkillEditVM vm)
        {
            if (id != vm.Id) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            var dup = await _db.Skills
                .AnyAsync(x => x.Id != id && x.Name.ToUpper() == vm.Name!.Trim().ToUpper());
            if (dup)
            {
                ModelState.AddModelError(nameof(vm.Name), "Tên kỹ năng đã tồn tại.");
                return View(vm);
            }

            s.Name = vm.Name!.Trim();
            s.Description = vm.Description;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật kỹ năng.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Skills/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            return View(new SkillDetailsVM
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            });
        }

        // POST: /Admin/Skills/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var s = await _db.Skills.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            _db.Skills.Remove(s);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xoá kỹ năng.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ===== ViewModels =====
    public class SkillListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
    }

    public class SkillIndexVM
    {
        public List<SkillListItemVM> Items { get; set; } = new();
        public string? Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    }

    public class SkillEditVM
    {
        public int? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "Tên kỹ năng")]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }

    public class SkillDetailsVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
    }
}
