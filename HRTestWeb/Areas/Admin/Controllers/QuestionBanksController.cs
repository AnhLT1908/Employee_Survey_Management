using System;
using System.Linq;
using System.Threading.Tasks;
using HRTestDomain.Entities;
using HRTestInfrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuestionBanksController : Controller
    {
        private readonly HRTestDbContext _db;
        private const int PAGE_SIZE = 10;

        public QuestionBanksController(HRTestDbContext db)
        {
            _db = db;
        }

        // GET: /Admin/QuestionBanks
        public async Task<IActionResult> Index(string? q, int? skillId, int page = 1)
        {
            if (page < 1) page = 1;

            // Base: tất cả banks
            IQueryable<QuestionBank> banks = _db.QuestionBanks;

            // Lọc theo kỹ năng: lấy bankId của các câu hỏi có SkillId
            if (skillId.HasValue)
            {
                var bankIds = _db.Questions
                    .Where(x => x.SkillId == skillId.Value)
                    .Select(x => x.BankId)
                    .Distinct();

                banks = banks.Where(b => bankIds.Contains(b.Id));
            }

            // Search theo tên/mô tả
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpper();
                banks = banks.Where(b =>
                    b.Name.ToUpper().Contains(k) ||
                    (b.Description != null && b.Description.ToUpper().Contains(k)));
            }

            var totalItems = await banks.CountAsync();

            var pageItems = await banks
                .OrderBy(b => b.Name)
                .Skip((page - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .AsNoTracking()
                .ToListAsync();

            var pageIds = pageItems.Select(x => x.Id).ToList();

            // Đếm số câu hỏi trong từng bank (để hiển thị)
            var qCounts = await _db.Questions
                .Where(qs => pageIds.Contains(qs.BankId))
                .GroupBy(qs => qs.BankId)
                .Select(g => new { BankId = g.Key, Cnt = g.Count() })
                .ToListAsync();

            var items = pageItems.Select(b => new QuestionBankListItemVM
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                QuestionCount = qCounts.FirstOrDefault(x => x.BankId == b.Id)?.Cnt ?? 0
            }).ToList();

            var vm = new QuestionBankIndexVM
            {
                Items = items,
                Q = q,
                SkillId = skillId,
                Page = page,
                PageSize = PAGE_SIZE,
                TotalItems = totalItems
            };

            // Dropdown kỹ năng
            vm.Skills = await _db.Skills
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name,
                    Selected = skillId.HasValue && s.Id == skillId.Value
                })
                .ToListAsync();
            vm.Skills.Insert(0, new SelectListItem { Value = "", Text = "Tất cả kỹ năng" });

            return View(vm);
        }

        // GET: /Admin/QuestionBanks/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var b = await _db.QuestionBanks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            var count = await _db.Questions.CountAsync(x => x.BankId == id);

            return View(new QuestionBankDetailsVM
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                QuestionCount = count
            });
        }

        // GET: /Admin/QuestionBanks/Create
        public IActionResult Create() => View(new QuestionBankEditVM());

        // POST: /Admin/QuestionBanks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuestionBankEditVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var dup = await _db.QuestionBanks.AnyAsync(x => x.Name.ToUpper() == vm.Name!.Trim().ToUpper());
            if (dup)
            {
                ModelState.AddModelError(nameof(vm.Name), "Tên ngân hàng đã tồn tại.");
                return View(vm);
            }

            _db.QuestionBanks.Add(new QuestionBank
            {
                Name = vm.Name!.Trim(),
                Description = vm.Description
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã tạo ngân hàng câu hỏi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/QuestionBanks/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var b = await _db.QuestionBanks.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            return View(new QuestionBankEditVM
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description
            });
        }

        // POST: /Admin/QuestionBanks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QuestionBankEditVM vm)
        {
            if (id != vm.Id) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var b = await _db.QuestionBanks.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            var dup = await _db.QuestionBanks
                .AnyAsync(x => x.Id != id && x.Name.ToUpper() == vm.Name!.Trim().ToUpper());
            if (dup)
            {
                ModelState.AddModelError(nameof(vm.Name), "Tên ngân hàng đã tồn tại.");
                return View(vm);
            }

            b.Name = vm.Name!.Trim();
            b.Description = vm.Description;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật ngân hàng câu hỏi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/QuestionBanks/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var b = await _db.QuestionBanks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            var count = await _db.Questions.CountAsync(x => x.BankId == id);

            return View(new QuestionBankDetailsVM
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                QuestionCount = count
            });
        }

        // POST: /Admin/QuestionBanks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var b = await _db.QuestionBanks.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            // Không cho xoá nếu còn câu hỏi
            var count = await _db.Questions.CountAsync(x => x.BankId == id);
            if (count > 0)
            {
                TempData["Error"] = "Không thể xoá: vẫn còn câu hỏi thuộc ngân hàng này.";
                return RedirectToAction(nameof(Index));
            }

            _db.QuestionBanks.Remove(b);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xoá ngân hàng câu hỏi.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ===== ViewModels =====
    public class QuestionBankListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int QuestionCount { get; set; }
    }

    public class QuestionBankIndexVM
    {
        public List<QuestionBankListItemVM> Items { get; set; } = new();
        public string? Q { get; set; }
        public int? SkillId { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));

        public List<SelectListItem> Skills { get; set; } = new();
    }

    public class QuestionBankEditVM
    {
        public int? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "Tên ngân hàng")]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }

    public class QuestionBankDetailsVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int QuestionCount { get; set; }
    }
}
