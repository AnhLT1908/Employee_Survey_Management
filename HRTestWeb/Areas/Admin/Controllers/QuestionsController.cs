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
    public class QuestionsController : Controller
    {
        private readonly HRTestDbContext _db;
        private const int PAGE_SIZE = 10;

        public QuestionsController(HRTestDbContext db) => _db = db;

        // GET: /Admin/Questions
        public async Task<IActionResult> Index(string? q, int? bankId, int? skillId, int? type, int? difficulty, int page = 1)
        {
            if (page < 1) page = 1;

            var filtered = _db.Questions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                filtered = filtered.Where(x => x.Content.Contains(k));
            }
            if (bankId.HasValue) filtered = filtered.Where(x => x.BankId == bankId.Value);
            if (skillId.HasValue) filtered = filtered.Where(x => x.SkillId == skillId.Value);
            if (type.HasValue) filtered = filtered.Where(x => x.Type == type.Value);
            if (difficulty.HasValue) filtered = filtered.Where(x => x.Difficulty == difficulty.Value);

            var totalItems = await filtered.CountAsync();

            var items = await (
                from x in filtered
                join b in _db.QuestionBanks on x.BankId equals b.Id
                join s in _db.Skills on x.SkillId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                orderby x.Id
                select new QuestionListItemVM
                {
                    Id = x.Id,
                    Content = x.Content,
                    BankId = x.BankId,
                    BankName = b.Name,
                    SkillId = x.SkillId,
                    SkillName = s != null ? s.Name : null,
                    Type = x.Type,
                    Difficulty = x.Difficulty,
                    Score = x.Score
                })
                .Skip((page - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .AsNoTracking()
                .ToListAsync();

            var vm = new QuestionIndexVM
            {
                Items = items,
                Q = q,
                BankId = bankId,
                SkillId = skillId,
                Type = type,
                Difficulty = difficulty,
                Page = page,
                PageSize = PAGE_SIZE,
                TotalItems = totalItems
            };

            await FillLookups(vm);
            return View(vm);
        }

        // GET: /Admin/Questions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var q = await _db.Questions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            var bankName = await _db.QuestionBanks.Where(b => b.Id == q.BankId).Select(b => b.Name).FirstOrDefaultAsync();
            var skillName = await _db.Skills.Where(s => s.Id == q.SkillId).Select(s => s.Name).FirstOrDefaultAsync();

            return View(new QuestionDetailsVM
            {
                Id = q.Id,
                Content = q.Content,
                BankName = bankName!,
                SkillName = skillName,
                Type = q.Type,
                Difficulty = q.Difficulty,
                Score = q.Score,
                OptionsJson = q.OptionsJson,
                CorrectAnswerJson = q.CorrectAnswerJson
            });
        }

        // GET: /Admin/Questions/Create
        public async Task<IActionResult> Create()
        {
            var vm = new QuestionEditVM { Score = 1m, Type = 0, Difficulty = 0 };
            await FillLookups(vm);
            return View(vm);
        }

        // POST: /Admin/Questions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuestionEditVM vm)
        {
            await FillLookups(vm);
            if (!ModelState.IsValid) return View(vm);

            var entity = new Question
            {
                BankId = vm.BankId!.Value,
                SkillId = vm.SkillId,
                Content = vm.Content!,
                Type = vm.Type!.Value,          // 0..2
                Difficulty = vm.Difficulty!.Value, // 0..2
                Score = vm.Score!.Value,
                OptionsJson = string.IsNullOrWhiteSpace(vm.OptionsJson) ? null : vm.OptionsJson,
                CorrectAnswerJson = string.IsNullOrWhiteSpace(vm.CorrectAnswerJson) ? null : vm.CorrectAnswerJson
            };

            _db.Questions.Add(entity);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã tạo câu hỏi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Questions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            var vm = new QuestionEditVM
            {
                Id = q.Id,
                BankId = q.BankId,
                SkillId = q.SkillId,
                Content = q.Content,
                Type = q.Type,
                Difficulty = q.Difficulty,
                Score = q.Score,
                OptionsJson = q.OptionsJson,
                CorrectAnswerJson = q.CorrectAnswerJson
            };
            await FillLookups(vm);
            return View(vm);
        }

        // POST: /Admin/Questions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QuestionEditVM vm)
        {
            await FillLookups(vm);
            if (id != vm.Id) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            q.BankId = vm.BankId!.Value;
            q.SkillId = vm.SkillId;
            q.Content = vm.Content!;
            q.Type = vm.Type!.Value;
            q.Difficulty = vm.Difficulty!.Value;
            q.Score = vm.Score!.Value;
            q.OptionsJson = string.IsNullOrWhiteSpace(vm.OptionsJson) ? null : vm.OptionsJson;
            q.CorrectAnswerJson = string.IsNullOrWhiteSpace(vm.CorrectAnswerJson) ? null : vm.CorrectAnswerJson;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật câu hỏi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Questions/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var row = await (
                from x in _db.Questions
                join b in _db.QuestionBanks on x.BankId equals b.Id
                join s in _db.Skills on x.SkillId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                where x.Id == id
                select new QuestionDetailsVM
                {
                    Id = x.Id,
                    Content = x.Content,
                    BankName = b.Name,
                    SkillName = s != null ? s.Name : null,
                    Type = x.Type,
                    Difficulty = x.Difficulty,
                    Score = x.Score,
                    OptionsJson = x.OptionsJson,
                    CorrectAnswerJson = x.CorrectAnswerJson
                }).AsNoTracking().FirstOrDefaultAsync();

            if (row == null) return NotFound();
            return View(row);
        }

        // POST: /Admin/Questions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            _db.Questions.Remove(q);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xoá câu hỏi.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Lookups (banks/skills/types/difficulties) =====
        private async Task FillLookups(QuestionIndexBaseVM vm)
        {
            vm.Banks = await _db.QuestionBanks
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = vm.BankId.HasValue && vm.BankId.Value == x.Id
                }).ToListAsync();
            if (vm is QuestionIndexVM) vm.Banks.Insert(0, new SelectListItem("Tất cả ngân hàng", ""));

            vm.Skills = await _db.Skills
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = vm.SkillId.HasValue && vm.SkillId.Value == x.Id
                }).ToListAsync();
            if (vm is QuestionIndexVM) vm.Skills.Insert(0, new SelectListItem("Tất cả kỹ năng", ""));

            vm.TypeItems = new()
            {
                new SelectListItem("MCQ", "0", vm.Type == 0),
                new SelectListItem("Tự luận", "1", vm.Type == 1),
                new SelectListItem("Đúng/Sai", "2", vm.Type == 2)
            };
            if (vm is QuestionIndexVM) vm.TypeItems.Insert(0, new SelectListItem("Tất cả loại", ""));

            vm.DifficultyItems = new()
{
    new SelectListItem("Intern", "0", vm.Difficulty == 0),
    new SelectListItem("Fresher", "1", vm.Difficulty == 1),
    new SelectListItem("Junior", "2", vm.Difficulty == 2),
    new SelectListItem("Middle", "3", vm.Difficulty == 3),
    new SelectListItem("Senior", "4", vm.Difficulty == 4),
    new SelectListItem("Lead", "5", vm.Difficulty == 5),
    new SelectListItem("Manager", "6", vm.Difficulty == 6)
};
            if (vm is QuestionIndexVM) vm.DifficultyItems.Insert(0, new SelectListItem("Tất cả độ khó", ""));

        }
    }

    // ===== ViewModels =====
    public class QuestionListItemVM
    {
        public int Id { get; set; }
        public string Content { get; set; } = default!;
        public int BankId { get; set; }
        public string BankName { get; set; } = default!;
        public int? SkillId { get; set; }
        public string? SkillName { get; set; }
        public int Type { get; set; }
        public int Difficulty { get; set; }
        public decimal Score { get; set; }
    }

    public abstract class QuestionIndexBaseVM
    {
        public int? BankId { get; set; }
        public int? SkillId { get; set; }
        public int? Type { get; set; }
        public int? Difficulty { get; set; }

        public List<SelectListItem> Banks { get; set; } = new();
        public List<SelectListItem> Skills { get; set; } = new();
        public List<SelectListItem> TypeItems { get; set; } = new();
        public List<SelectListItem> DifficultyItems { get; set; } = new();
    }

    public class QuestionIndexVM : QuestionIndexBaseVM
    {
        public List<QuestionListItemVM> Items { get; set; } = new();
        public string? Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    }

    public class QuestionEditVM : QuestionIndexBaseVM
    {
        public int? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Display(Name = "Ngân hàng câu hỏi")]
        public new int? BankId { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Kỹ năng")]
        public new int? SkillId { get; set; }

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Display(Name = "Nội dung")]
        public string? Content { get; set; }

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Range(0, 2), System.ComponentModel.DataAnnotations.Display(Name = "Loại câu hỏi")]
        public new int? Type { get; set; }          // 0 MCQ, 1 Essay, 2 TrueFalse

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Range(0, 6), System.ComponentModel.DataAnnotations.Display(Name = "Độ khó")]
        public new int? Difficulty { get; set; }     // 0 Junior, 1 Middle, 2 Senior

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Range(0.01, 1000), System.ComponentModel.DataAnnotations.Display(Name = "Điểm")]
        public decimal? Score { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Options (JSON)")]
        public string? OptionsJson { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Đáp án đúng (JSON)")]
        public string? CorrectAnswerJson { get; set; }
    }

    public class QuestionDetailsVM
    {
        public int Id { get; set; }
        public string Content { get; set; } = default!;
        public string BankName { get; set; } = default!;
        public string? SkillName { get; set; }
        public int Type { get; set; }
        public int Difficulty { get; set; }
        public decimal Score { get; set; }
        public string? OptionsJson { get; set; }
        public string? CorrectAnswerJson { get; set; }
    }
}
