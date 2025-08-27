using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using HRTestDomain.Entities;
using HRTestInfrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TestsController : Controller
    {
        private readonly HRTestDbContext _db;
        private readonly RoleManager<IdentityRole> _roleMgr;
        private const int PAGE_SIZE = 10;

        public TestsController(HRTestDbContext db, RoleManager<IdentityRole> roleMgr)
        {
            _db = db;
            _roleMgr = roleMgr;
        }

        // ========== LIST ==========
        // GET: /Admin/Tests
        public async Task<IActionResult> Index(string? q, string? role, int page = 1)
        {
            if (page < 1) page = 1;

            var baseQuery = _db.Tests
                .Select(t => new
                {
                    Test = t,
                    QuestionCount = _db.TestQuestions.Count(x => x.TestId == t.Id)
                })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpper();
                baseQuery = baseQuery.Where(x =>
                    (x.Test.Name != null && x.Test.Name.ToUpper().Contains(k)) ||
                    (x.Test.Description != null && x.Test.Description.ToUpper().Contains(k)));
            }

            // lọc theo Role (bỏ Admin)
            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(x =>
                    _db.Assignments.Any(a =>
                        a.TestId == x.Test.Id &&
                        a.TargetType == "Role" &&
                        a.TargetValue.StartsWith(role + "|"))); // quy ước TargetValue = "RoleName|LevelId"
            }

            var total = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderByDescending(x => x.Test.Id)
                .Skip((page - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .Select(x => new TestListItemVM
                {
                    Id = x.Test.Id,
                    Name = x.Test.Name!,
                    Description = x.Test.Description,
                    DurationMinutes = x.Test.DurationMinutes,
                    PassScore = x.Test.PassScore,
                    QuestionCount = x.QuestionCount
                })
                .AsNoTracking()
                .ToListAsync();

            var roles = await _roleMgr.Roles
                .Where(r => r.Name != "Admin")
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            return View(new TestIndexVM
            {
                Items = items,
                Q = q,
                Role = role,
                Page = page,
                PageSize = PAGE_SIZE,
                TotalItems = total,
                Roles = roles
            });
        }

        // ========== CREATE ==========
        // GET: /Admin/Tests/Create
        public async Task<IActionResult> Create()
        {
            var vm = new TestCreateVM
            {
                DurationMinutes = 60,
                PassScore = 5m,
                QuestionCount = 10,
                Banks = await _db.QuestionBanks
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                    .ToListAsync(),
                Levels = await _db.Levels
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                    .ToListAsync(),
                Roles = await _roleMgr.Roles
                    .Where(r => r.Name != "Admin")
                    .OrderBy(r => r.Name)
                    .Select(r => new SelectListItem(r.Name!, r.Name!))
                    .ToListAsync()
            };
            return View(vm);
        }

        // POST: /Admin/Tests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TestCreateVM vm)
        {
            async Task FillLists()
            {
                vm.Banks ??= await _db.QuestionBanks.OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
                vm.Levels ??= await _db.Levels.OrderBy(x => x.Name)
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
                vm.Roles ??= await _roleMgr.Roles.Where(r => r.Name != "Admin").OrderBy(r => r.Name)
                    .Select(r => new SelectListItem(r.Name!, r.Name!)).ToListAsync();
            }

            if (!ModelState.IsValid)
            {
                await FillLists();
                return View(vm);
            }

            // Lấy nguồn câu hỏi theo ngân hàng
            var qQuery = _db.Questions.Where(q => q.BankId == vm.BankId);

            // (tuỳ chọn) lọc thêm theo Difficulty và Type
            if (vm.Difficulty.HasValue) qQuery = qQuery.Where(q => q.Difficulty == vm.Difficulty.Value);
            if (vm.Type.HasValue) qQuery = qQuery.Where(q => q.Type == vm.Type.Value);

            var totalAvailable = await qQuery.CountAsync();
            if (totalAvailable == 0)
            {
                ModelState.AddModelError(nameof(vm.BankId), "Ngân hàng không có câu hỏi phù hợp.");
                await FillLists();
                return View(vm);
            }

            var take = Math.Min(vm.QuestionCount, totalAvailable);

            // Random câu hỏi (SQL Server: ORDER BY NEWID())
            var picked = await qQuery
                .OrderBy(_ => Guid.NewGuid())
                .Take(take)
                .Select(q => q.Id)
                .ToListAsync();

            using var tx = await _db.Database.BeginTransactionAsync();

            var test = new Test
            {
                Name = vm.Name!.Trim(),
                Description = vm.Description,
                DurationMinutes = vm.DurationMinutes,
                PassScore = vm.PassScore,
                IsRandomized = true,
                CreatedBy = User.Identity?.Name
            };
            _db.Tests.Add(test);
            await _db.SaveChangesAsync();

            int order = 1;
            foreach (var qid in picked)
            {
                _db.TestQuestions.Add(new TestQuestion
                {
                    TestId = test.Id,
                    QuestionId = qid,
                    Order = order++
                });
            }

            // Gán cho Role + Level (nếu chọn)
            if (vm.SelectedRoles != null && vm.SelectedRoles.Any() && vm.LevelId.HasValue)
            {
                foreach (var role in vm.SelectedRoles)
                {
                    _db.Assignments.Add(new Assignment
                    {
                        TestId = test.Id,
                        TargetType = "Role",
                        TargetValue = $"{role}|{vm.LevelId.Value}",
                        StartAt = DateTime.UtcNow,
                        EndAt = DateTime.UtcNow.AddMonths(1),
                        IsActive = true
                    });
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = "Đã tạo bài test và sinh câu hỏi ngẫu nhiên.";
            return RedirectToAction(nameof(Index));
        }

        // ========== DETAILS ==========
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var test = await _db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            var qList = await _db.TestQuestions
                .Where(tq => tq.TestId == id)
                .Join(_db.Questions, tq => tq.QuestionId, q => q.Id, (tq, q) => new { tq.Order, Q = q })
                .OrderBy(x => x.Order)
                .ToListAsync();

            var bankIds = qList.Select(x => x.Q.BankId).Distinct().ToList();
            var bankNames = await _db.QuestionBanks
                .Where(b => bankIds.Contains(b.Id))
                .Select(b => b.Name)
                .ToListAsync();

            var skillIds = qList.Select(x => x.Q.SkillId).Where(sid => sid.HasValue).Select(sid => sid!.Value).Distinct().ToList();
            var skillDict = await _db.Skills
                .Where(s => skillIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            var assigns = await _db.Assignments
                .Where(a => a.TestId == id && a.TargetType == "Role")
                .ToListAsync();

            var levelIds = assigns
                .Select(a => { var parts = (a.TargetValue ?? "").Split('|'); return parts.Length == 2 && int.TryParse(parts[1], out var lv) ? (int?)lv : null; })
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

            var levelDict = await _db.Levels
                .Where(l => levelIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Name);

            string DiffName(int d) => d switch
            {
                0 => "Intern",
                1 => "Fresher",
                2 => "Junior",
                3 => "Middle",
                4 => "Senior",
                5 => "Lead",
                6 => "Manager",
                _ => "N/A"
            };

            string TypeName(int t) => t switch
            {
                0 => "Trắc nghiệm (MCQ)",
                1 => "Tự luận",
                2 => "Đúng/Sai",
                _ => "Khác"
            };

            var vm = new TestDetailsVM
            {
                Id = test.Id,
                Name = test.Name,
                Description = test.Description,
                DurationMinutes = test.DurationMinutes,
                PassScore = test.PassScore,
                IsRandomized = test.IsRandomized,
                CreatedBy = test.CreatedBy,
                QuestionCount = qList.Count,
                Banks = bankNames,
                RoleAssignments = assigns.Select(a =>
                {
                    var parts = (a.TargetValue ?? "").Split('|');
                    var roleName = parts.Length > 0 ? parts[0] : "";
                    int? levelId = parts.Length == 2 && int.TryParse(parts[1], out var lv) ? lv : null;
                    levelDict.TryGetValue(levelId ?? -1, out var levelName);
                    return new RoleAssignmentVM
                    {
                        RoleName = roleName,
                        LevelName = levelName,
                        StartAt = a.StartAt,
                        EndAt = a.EndAt,
                        IsActive = a.IsActive
                    };
                }).ToList(),
                Questions = qList.Select(x => new QuestionItemVM
                {
                    Order = x.Order,
                    Id = x.Q.Id,
                    Content = x.Q.Content,
                    TypeName = TypeName(x.Q.Type),
                    DifficultyName = DiffName(x.Q.Difficulty),
                    SkillName = x.Q.SkillId.HasValue && skillDict.ContainsKey(x.Q.SkillId.Value) ? skillDict[x.Q.SkillId.Value] : null,
                    Score = x.Q.Score
                }).ToList()
            };

            return View(vm);
        }
    }

    // ===== ViewModels =====
    public class TestListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public decimal PassScore { get; set; }
        public int QuestionCount { get; set; }
    }

    public class TestIndexVM
    {
        public List<TestListItemVM> Items { get; set; } = new();
        public string? Q { get; set; }
        public string? Role { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
        public List<string> Roles { get; set; } = new();
    }

    public class TestCreateVM
    {
        [Required, Display(Name = "Tên bài test")]
        public string? Name { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Range(1, 1000), Display(Name = "Thời gian (phút)")]
        public int DurationMinutes { get; set; }

        [Range(0, 1000), Display(Name = "Điểm qua môn")]
        public decimal PassScore { get; set; }

        [Required, Display(Name = "Ngân hàng câu hỏi")]
        public int BankId { get; set; }

        [Range(1, 500), Display(Name = "Số câu hỏi")]
        public int QuestionCount { get; set; }

        [Display(Name = "Độ khó (tuỳ chọn)")]
        public int? Difficulty { get; set; }

        [Display(Name = "Loại câu hỏi (tuỳ chọn)")]
        public int? Type { get; set; }

        [Display(Name = "Áp dụng cho Roles")]
        public List<string>? SelectedRoles { get; set; }

        [Display(Name = "Level")]
        public int? LevelId { get; set; }

        public List<SelectListItem>? Banks { get; set; }
        public List<SelectListItem>? Levels { get; set; }
        public List<SelectListItem>? Roles { get; set; }
    }

    public class TestDetailsVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public decimal PassScore { get; set; }
        public bool IsRandomized { get; set; }
        public string? CreatedBy { get; set; }
        public int QuestionCount { get; set; }
        public List<string> Banks { get; set; } = new();
        public List<RoleAssignmentVM> RoleAssignments { get; set; } = new();
        public List<QuestionItemVM> Questions { get; set; } = new();
    }

    public class RoleAssignmentVM
    {
        public string? RoleName { get; set; }
        public string? LevelName { get; set; }
        public DateTime? StartAt { get; set; }   
        public DateTime? EndAt { get; set; }  
        public bool IsActive { get; set; }
    }


    public class QuestionItemVM
    {
        public int Order { get; set; }
        public int Id { get; set; }
        public string Content { get; set; } = default!;
        public string TypeName { get; set; } = default!;
        public string DifficultyName { get; set; } = default!;
        public string? SkillName { get; set; }
        public decimal Score { get; set; }
    }
}
