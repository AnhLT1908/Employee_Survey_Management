using System.Linq;
using System.Threading.Tasks;
using HRTestInfrastructure.Identity;
using HRTestInfrastructure.Data;                 
using HRTestWeb.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRTestWeb.Controllers
{
    [Authorize]
    [Route("[controller]")] 
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly SignInManager<ApplicationUser> _signInMgr;
        private readonly ILogger<ProfileController> _logger;
        private readonly HRTestDbContext _db;      

        public ProfileController(
            UserManager<ApplicationUser> userMgr,
            SignInManager<ApplicationUser> signInMgr,
            ILogger<ProfileController> logger,
            HRTestDbContext db)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
            _logger = logger;
            _db = db;
        }

        private static string Mask(string? s) => s is null ? "(null)" : $"*** (len={s.Length})";

        // Helper: dựng VM kèm tên phòng ban/level & roles
        private async Task<ProfilePageVM> BuildPageVM(ApplicationUser user,
                                                      ProfileEditVM? profileVm = null,
                                                      ChangePasswordVM? pwdVm = null)
        {
            var roles = await _userMgr.GetRolesAsync(user);

            string deptName = "";
            if (user.DepartmentId.HasValue)
            {
                deptName = await _db.Departments
                                    .Where(d => d.Id == user.DepartmentId.Value)
                                    .Select(d => d.Name)
                                    .FirstOrDefaultAsync() ?? $"ID: {user.DepartmentId}";
            }
            else deptName = "(Chưa gán)";

            string levelName = "";
            if (user.LevelId.HasValue)
            {
                levelName = await _db.Levels
                                     .Where(l => l.Id == user.LevelId.Value)
                                     .Select(l => l.Name)        
                                     .FirstOrDefaultAsync() ?? $"Level {user.LevelId}";
            }
            else levelName = "(Chưa gán)";

            var prof = profileVm ?? new ProfileEditVM
            {
                FullName = user.FullName,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber ?? ""
            };

            prof.DepartmentDisplay = deptName;
            prof.LevelDisplay = levelName;
            prof.Roles = roles.ToList();

            return new ProfilePageVM
            {
                Profile = prof,
                ChangePassword = pwdVm ?? new ChangePasswordVM()
            };
        }

        [HttpGet, Route(""), Route("Index")]
        public async Task<IActionResult> Index()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null) return NotFound();

            ViewData["ActiveTab"] = TempData["ActiveTab"] as string ?? "profile";
            return View(await BuildPageVM(user));
        }

        [HttpPost, ValidateAntiForgeryToken, Route("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileEditVM vm)
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null) return NotFound();

            _logger.LogInformation("UpdateProfile by {Uid}: FullName={Full}, UserName={U}, Email={E}, Phone={P}",
                user.Id, vm.FullName, vm.UserName, vm.Email, vm.PhoneNumber);

            if (!ModelState.IsValid)
            {
                ViewData["ActiveTab"] = "profile";
                return View("Index", await BuildPageVM(user, vm));
            }

            // FullName
            if (!string.Equals(user.FullName, vm.FullName))
                user.FullName = vm.FullName?.Trim();

            // UserName
            if (!string.Equals(user.UserName, vm.UserName, System.StringComparison.OrdinalIgnoreCase))
            {
                var setUserName = await _userMgr.SetUserNameAsync(user, vm.UserName.Trim());
                if (!setUserName.Succeeded)
                {
                    foreach (var e in setUserName.Errors)
                        ModelState.AddModelError(nameof(vm.UserName), e.Description);
                    ViewData["ActiveTab"] = "profile";
                    return View("Index", await BuildPageVM(user, vm));
                }
            }

            // Email
            if (!string.Equals(user.Email, vm.Email))
            {
                var setEmail = await _userMgr.SetEmailAsync(user, vm.Email ?? "");
                if (!setEmail.Succeeded)
                {
                    foreach (var e in setEmail.Errors)
                        ModelState.AddModelError(nameof(vm.Email), e.Description);
                    ViewData["ActiveTab"] = "profile";
                    return View("Index", await BuildPageVM(user, vm));
                }
            }

            // Phone
            var curPhone = await _userMgr.GetPhoneNumberAsync(user);
            if (!string.Equals(curPhone, vm.PhoneNumber))
            {
                var setPhone = await _userMgr.SetPhoneNumberAsync(user, vm.PhoneNumber ?? "");
                if (!setPhone.Succeeded)
                {
                    foreach (var e in setPhone.Errors)
                        ModelState.AddModelError(nameof(vm.PhoneNumber), e.Description);
                    ViewData["ActiveTab"] = "profile";
                    return View("Index", await BuildPageVM(user, vm));
                }
            }

            var upd = await _userMgr.UpdateAsync(user);
            if (!upd.Succeeded)
            {
                foreach (var e in upd.Errors) ModelState.AddModelError("", e.Description);
                ViewData["ActiveTab"] = "profile";
                return View("Index", await BuildPageVM(user, vm));
            }

            await _signInMgr.RefreshSignInAsync(user);
            TempData["Success"] = "Đã cập nhật hồ sơ.";
            return RedirectToAction("Index", "Profile");
        }

        [HttpPost, ValidateAntiForgeryToken, Route("ChangePassword")]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordVM vm)
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null) return NotFound();

            _logger.LogInformation("ChangePassword POST by {Uid}/{Uname}: Current={Cur}, New={New}, Confirm={Conf}",
                user.Id, user.UserName, Mask(vm.CurrentPassword), Mask(vm.NewPassword), Mask(vm.ConfirmPassword));

            if (!ModelState.IsValid)
            {
                ViewData["ActiveTab"] = "password";
                return View("Index", await BuildPageVM(user, null, vm));
            }

            if (!await _userMgr.CheckPasswordAsync(user, vm.CurrentPassword ?? ""))
            {
                ModelState.AddModelError(nameof(vm.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                ViewData["ActiveTab"] = "password";
                return View("Index", await BuildPageVM(user, null, vm));
            }

            var result = await _userMgr.ChangePasswordAsync(user, vm.CurrentPassword!, vm.NewPassword!);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                ViewData["ActiveTab"] = "password";
                return View("Index", await BuildPageVM(user, null, vm));
            }

            await _signInMgr.RefreshSignInAsync(user);
            TempData["Success"] = "Đổi mật khẩu thành công.";
            TempData["ActiveTab"] = "password";
            return Redirect(Url.Action("Index", "Profile") + "#tab-password");
        }
    }
}
