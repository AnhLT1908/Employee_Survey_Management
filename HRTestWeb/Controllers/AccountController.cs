using System.Security.Claims;
using System.Threading.Tasks;
using HRTestInfrastructure.Identity;
using HRTestWeb.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HRTestWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email)
                       ?? await _userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm bị khóa do đăng nhập sai nhiều lần.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // ----- SSO: gọi provider bên ngoài -----
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(props, provider);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (!string.IsNullOrEmpty(remoteError))
            {
                TempData["Error"] = $"SSO lỗi: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction(nameof(Login), new { returnUrl });

            // Nếu đã liên kết thì đăng nhập luôn
            var extSignIn = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (extSignIn.Succeeded)
                return Redirect(returnUrl ?? Url.Action("Index", "Home")!);

            // Nếu chưa có user => tạo mới từ info SSO (dùng email)
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "SSO không trả về email.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = info.Principal.Identity?.Name
                };
                var createRes = await _userManager.CreateAsync(user);
                if (!createRes.Succeeded)
                {
                    TempData["Error"] = "Không tạo được tài khoản từ SSO.";
                    return RedirectToAction(nameof(Login));
                }
            }

            await _userManager.AddLoginAsync(user, info);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Redirect(returnUrl ?? Url.Action("Index", "Home")!);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ForgotPassword() => Content("Chức năng quên mật khẩu sẽ được bổ sung.");
    }
}
