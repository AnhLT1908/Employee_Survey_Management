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

            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(x =>
                    _db.Assignments.Any(a =>
                        a.TestId == x.Test.Id &&
                        a.TargetType == "Role" &&
                        a.TargetValue.StartsWith(role + "|")));
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
        [HttpGet]
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

            // validate thời gian (nếu cả 2 cùng nhập)
            if (vm.StartAt.HasValue && vm.EndAt.HasValue && vm.StartAt > vm.EndAt)
            {
                ModelState.AddModelError(nameof(vm.EndAt), "Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            if (!ModelState.IsValid)
            {
                await FillLists();
                return View(vm);
            }

            // Lấy nguồn câu hỏi theo ngân hàng
            var qQuery = _db.Questions.Where(q => q.BankId == vm.BankId);

            // (tuỳ chọn) lọc Difficulty & Type
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

            // Gán cho Role + Level + thời gian hiệu lực
            if (vm.SelectedRoles != null && vm.SelectedRoles.Any() && vm.LevelId.HasValue)
            {
                // chuyển sang UTC nếu người dùng nhập
                DateTime? startUtc = vm.StartAt.HasValue
                    ? DateTime.SpecifyKind(vm.StartAt.Value, DateTimeKind.Local).ToUniversalTime()
                    : (DateTime?)null;
                DateTime? endUtc = vm.EndAt.HasValue
                    ? DateTime.SpecifyKind(vm.EndAt.Value, DateTimeKind.Local).ToUniversalTime()
                    : (DateTime?)null;

                foreach (var role in vm.SelectedRoles)
                {
                    _db.Assignments.Add(new Assignment
                    {
                        TestId = test.Id,
                        TargetType = "Role",
                        TargetValue = $"{role}|{vm.LevelId.Value}",
                        StartAt = startUtc ?? DateTime.UtcNow, // StartAt là non-nullable
                        EndAt = endUtc,
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

            // Lấy câu hỏi của test
            var qList = await _db.TestQuestions
                .Where(tq => tq.TestId == id)
                .Join(_db.Questions, tq => tq.QuestionId, q => q.Id, (tq, q) => new { tq.Order, Q = q })
                .OrderBy(x => x.Order)
                .ToListAsync();

            // Banks, Skills, Assignments, Levels...
            var bankIds = qList.Select(x => x.Q.BankId).Distinct().ToList();
            var bankNames = await _db.QuestionBanks
                .Where(b => bankIds.Contains(b.Id))
                .Select(b => b.Name)
                .ToListAsync();

            var skillIds = qList.Select(x => x.Q.SkillId).Where(sid => sid.HasValue)
                .Select(sid => sid!.Value).Distinct().ToList();
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

            // Timezone: đổi UTC -> giờ VN (hoặc timezone máy)
            string tzId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);

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

                // ✅ GÁN LẠI DANH SÁCH CÂU HỎI
                Questions = qList.Select(x => new QuestionItemVM
                {
                    Order = x.Order,
                    Id = x.Q.Id,
                    Content = x.Q.Content,
                    TypeName = TypeName(x.Q.Type),
                    DifficultyName = DiffName(x.Q.Difficulty),
                    SkillName = x.Q.SkillId.HasValue && skillDict.ContainsKey(x.Q.SkillId.Value)
                                ? skillDict[x.Q.SkillId.Value] : null,
                    Score = x.Q.Score
                }).ToList(),

                // Role/Level + đổi thời gian UTC -> Local
                RoleAssignments = assigns.Select(a =>
                {
                    var parts = (a.TargetValue ?? "").Split('|');
                    var roleName = parts.Length > 0 ? parts[0] : "";
                    int? levelId = parts.Length == 2 && int.TryParse(parts[1], out var lv) ? lv : null;
                    levelDict.TryGetValue(levelId ?? -1, out var levelName);

                    var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(a.StartAt, DateTimeKind.Utc), tz);

                    DateTime? endLocal = a.EndAt.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.SpecifyKind(a.EndAt.Value, DateTimeKind.Utc), tz)
                        : (DateTime?)null;

                    return new RoleAssignmentVM
                    {
                        RoleName = roleName,
                        LevelName = levelName,
                        StartAt = startLocal,
                        EndAt = endLocal,
                        IsActive = a.IsActive
                    };
                }).ToList()
            };

            return View(vm);
        }

        // ===================== EDIT =====================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            // Câu hỏi hiện có của test (để hiển thị số lượng, và lấy BankId mặc định)
            var qList = await _db.TestQuestions
                .Where(tq => tq.TestId == id)
                .Join(_db.Questions, tq => tq.QuestionId, q => q.Id, (tq, q) => q)
                .ToListAsync();

            // BankId: giả định test được sinh từ 1 bank (như lúc Create)
            // Nếu có nhiều bank thì chọn bank đầu tiên chỉ để prefill dropdown.
            var bankId = qList.Select(q => q.BankId).Distinct().FirstOrDefault();

            // Lấy các Assignment theo Role để prefill Role/Level/Thời gian
            var assigns = await _db.Assignments
                .Where(a => a.TestId == id && a.TargetType == "Role")
                .ToListAsync();

            // Các role đang áp dụng
            var selectedRoles = assigns
                .Select(a => (a.TargetValue ?? "").Split('|').FirstOrDefault())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            // Level: lấy theo assignment đầu tiên (thiết kế đang dùng 1 level chung cho test)
            int? levelId = null;
            var first = assigns.FirstOrDefault();
            if (first != null)
            {
                var parts = (first.TargetValue ?? "").Split('|');
                if (parts.Length == 2 && int.TryParse(parts[1], out var lv)) levelId = lv;
            }

            // đổi UTC -> Local để binding vào <input type="datetime-local">
            DateTime? startLocal = first != null
                ? DateTime.SpecifyKind(first.StartAt, DateTimeKind.Utc).ToLocalTime()
                : (DateTime?)null;
            DateTime? endLocal = first?.EndAt != null
                ? DateTime.SpecifyKind(first!.EndAt!.Value, DateTimeKind.Utc).ToLocalTime()
                : (DateTime?)null;

            var vm = new TestEditVM
            {
                Id = test.Id,
                Name = test.Name,
                Description = test.Description,
                DurationMinutes = test.DurationMinutes,
                PassScore = test.PassScore,
                BankId = bankId,                 // dùng để hiển thị mặc định
                QuestionCount = qList.Count,     // số câu hỏi hiện có
                CurrentQuestionCount = qList.Count,
                // Không ép Difficulty/Type vì test có thể trộn; để null là “không ràng buộc”
                Difficulty = null,
                Type = null,
                LevelId = levelId,
                SelectedRoles = selectedRoles,
                StartAt = startLocal,
                EndAt = endLocal,
                // danh mục
                Banks = await _db.QuestionBanks.OrderBy(x => x.Name)
                        .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Levels = await _db.Levels.OrderBy(x => x.Name)
                        .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Roles = await _roleMgr.Roles.Where(r => r.Name != "Admin").OrderBy(r => r.Name)
                        .Select(r => new SelectListItem(r.Name!, r.Name!)).ToListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TestEditVM vm)
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

            // Validate thời gian (nếu có nhập)
            if (vm.StartAt.HasValue && vm.EndAt.HasValue && vm.StartAt > vm.EndAt)
                ModelState.AddModelError(nameof(vm.EndAt), "Thời gian kết thúc phải sau thời gian bắt đầu.");

            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == vm.Id);
            if (test == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await FillLists();
                return View(vm);
            }

            using var tx = await _db.Database.BeginTransactionAsync();

            // Cập nhật thông tin chung của bài test
            test.Name = vm.Name!.Trim();
            test.Description = vm.Description;
            test.DurationMinutes = vm.DurationMinutes;
            test.PassScore = vm.PassScore;
            await _db.SaveChangesAsync();

            // =================== Re-generate câu hỏi (tuỳ chọn) ===================
            if (vm.RegenerateQuestions)
            {
                // Lấy nguồn câu hỏi theo bank + optional filters
                var qQuery = _db.Questions.Where(q => q.BankId == vm.BankId);
                if (vm.Difficulty.HasValue) qQuery = qQuery.Where(q => q.Difficulty == vm.Difficulty.Value);
                if (vm.Type.HasValue) qQuery = qQuery.Where(q => q.Type == vm.Type.Value);

                var total = await qQuery.CountAsync();
                if (total == 0)
                {
                    ModelState.AddModelError(nameof(vm.BankId), "Ngân hàng không có câu hỏi phù hợp.");
                    await FillLists();
                    return View(vm);
                }

                var take = Math.Min(vm.QuestionCount, total);
                var picked = await qQuery
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(take)
                    .Select(q => q.Id)
                    .ToListAsync();

                // Xoá câu hỏi cũ, thêm lại mới
                var old = await _db.TestQuestions.Where(tq => tq.TestId == test.Id).ToListAsync();
                _db.TestQuestions.RemoveRange(old);

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
                // “IsRandomized” cho biết test này được xáo trộn khi tạo/sửa
                test.IsRandomized = true;
                await _db.SaveChangesAsync();
            }

            // =================== Cập nhật Assignment theo Role/Level ===================
            // Xoá các assignment Role cũ, thêm lại theo form
            var oldAssigns = await _db.Assignments
                .Where(a => a.TestId == test.Id && a.TargetType == "Role").ToListAsync();
            _db.Assignments.RemoveRange(oldAssigns);

            if (vm.SelectedRoles != null && vm.SelectedRoles.Any() && vm.LevelId.HasValue)
            {
                // convert Local -> UTC để lưu
                DateTime startUtc = (vm.StartAt.HasValue
                                        ? DateTime.SpecifyKind(vm.StartAt.Value, DateTimeKind.Local)
                                        : DateTime.Now).ToUniversalTime();
                DateTime? endUtc = vm.EndAt.HasValue
                                        ? DateTime.SpecifyKind(vm.EndAt.Value, DateTimeKind.Local).ToUniversalTime()
                                        : (DateTime?)null;

                foreach (var role in vm.SelectedRoles)
                {
                    _db.Assignments.Add(new Assignment
                    {
                        TestId = test.Id,
                        TargetType = "Role",
                        TargetValue = $"{role}|{vm.LevelId.Value}",
                        StartAt = startUtc,   // cột StartAt là non-nullable
                        EndAt = endUtc,
                        IsActive = true
                    });
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = "Đã cập nhật bài test.";
            return RedirectToAction(nameof(Details), new { id = test.Id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound();

            // Nếu bạn muốn ngăn xoá khi đã có lượt làm, bật đoạn dưới:
            if (await _db.TestAttempts.AnyAsync(a => a.TestId == id))
            {
                TempData["Error"] = "Bài test đã có lượt làm. Không thể xoá.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var tx = await _db.Database.BeginTransactionAsync();

                // Feedback -> Test đang để DeleteBehavior.Restrict => cần xoá trước
                var feedbacks = await _db.Feedbacks.Where(f => f.TestId == id).ToListAsync();
                if (feedbacks.Count > 0) _db.Feedbacks.RemoveRange(feedbacks);
                await _db.SaveChangesAsync();

                // Các bảng khác đang cascade (TestQuestions, Assignments, TestAttempts, Answers…)
                _db.Tests.Remove(test);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                TempData["Success"] = "Đã xoá bài test.";
            }
            catch (DbUpdateException ex)
            {
                // Bắt lỗi ràng buộc nếu có dữ liệu chưa xử lý
                TempData["Error"] = "Không thể xoá bài test do đang được tham chiếu. " +
                                    "Vui lòng kiểm tra feedback/attempts liên quan. Chi tiết: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
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

        // Thời gian áp dụng cho Assignment (tuỳ chọn)
        [Display(Name = "Bắt đầu (tuỳ chọn)")]
        [DataType(DataType.DateTime)]
        public DateTime? StartAt { get; set; }

        [Display(Name = "Kết thúc (tuỳ chọn)")]
        [DataType(DataType.DateTime)]
        public DateTime? EndAt { get; set; }

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

    public class TestEditVM : TestCreateVM
    {
        public int Id { get; set; }

        [Display(Name = "Xáo trộn lại câu hỏi")]
        public bool RegenerateQuestions { get; set; } = false;

        // Chỉ để hiển thị tham khảo
        public int CurrentQuestionCount { get; set; }
    }

}
