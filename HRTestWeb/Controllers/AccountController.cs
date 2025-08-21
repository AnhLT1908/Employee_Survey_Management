using System.Linq;                          // gộp lỗi hiển thị
using System.Security.Claims;
using System.Security.Cryptography;         // tạo OTP
using System.Text.Json;                     // serialize token OTP
using System.Threading.Tasks;
using HRTestInfrastructure.Identity;
using HRTestWeb.Services.Email;             // IEmailSender
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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _email;

        private const string DefaultSsoRole = "Employee";
        private const string Provider = "HRTest";
        private const string OtpName = "ResetPasswordOtp";
        private const string ResetTokenName = "ResetPasswordToken";

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender email)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _email = email;
        }

        #region Helpers
        private static string NewOtp6() =>
            RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        private static string ToJson(object o) => JsonSerializer.Serialize(o);

        private static T? FromJson<T>(string? s) =>
            string.IsNullOrWhiteSpace(s) ? default : JsonSerializer.Deserialize<T>(s!);

        private async Task EnsureRoleAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
        #endregion

        #region Local login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
            => View(new LoginViewModel { ReturnUrl = returnUrl });

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
        #endregion

        #region SSO
        [HttpPost]
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
            if (info == null)
            {
                TempData["Error"] = "Không lấy được thông tin đăng nhập ngoài.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            // Nếu đã liên kết -> đăng nhập luôn
            var extSignIn = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (extSignIn.Succeeded)
                return Redirect(returnUrl ?? Url.Action("Index", "Home")!);

            // Lấy thông tin từ claims
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                        ?? info.Principal.FindFirstValue("preferred_username");
            var displayName = info.Principal.Identity?.Name
                              ?? info.Principal.FindFirstValue("name")
                              ?? email;

            string MakeUserName()
            {
                if (!string.IsNullOrWhiteSpace(email)) return email;
                var raw = $"ext-{info.LoginProvider}-{info.ProviderKey}";
                return new string(raw.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.').ToArray());
            }

            // Tìm user
            ApplicationUser? user = null;
            if (!string.IsNullOrWhiteSpace(email))
                user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                user = await _userManager.FindByNameAsync(MakeUserName());

            // Chưa có -> tạo mới
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = MakeUserName(),
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    EmailConfirmed = !string.IsNullOrWhiteSpace(email),
                    FullName = displayName
                };

                var createRes = await _userManager.CreateAsync(user);
                if (!createRes.Succeeded)
                {
                    var errs = string.Join("; ", createRes.Errors.Select(e => e.Description));
                    TempData["Error"] = $"Không tạo được tài khoản từ SSO: {errs}";
                    return RedirectToAction(nameof(Login));
                }

                // gán role mặc định
                await EnsureRoleAsync(DefaultSsoRole);
                if (!await _userManager.IsInRoleAsync(user, DefaultSsoRole))
                    await _userManager.AddToRoleAsync(user, DefaultSsoRole);
            }

            // Link thông tin đăng nhập ngoài
            var linkRes = await _userManager.AddLoginAsync(user, info);
            if (!linkRes.Succeeded && !linkRes.Errors.Any(e => e.Code.Contains("LoginAlreadyAssociated")))
            {
                var errs = string.Join("; ", linkRes.Errors.Select(e => e.Description));
                TempData["Error"] = $"Không liên kết được tài khoản SSO: {errs}";
                return RedirectToAction(nameof(Login));
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return Redirect(returnUrl ?? Url.Action("Index", "Home")!);
        }
        #endregion

        #region Logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
        #endregion

        #region Forgot Password (Email -> OTP -> New Password)
        // B1: Nhập email, gửi OTP (lưu trong AspNetUserTokens)
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            ViewData["Title"] = "Quên mật khẩu";
            return View(new ForgotPasswordEmailViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordEmailViewModel model)
        {
            ViewData["Title"] = "Quên mật khẩu";
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            // Luôn trả về thông báo thành công để tránh lộ thông tin tồn tại email
            if (user != null)
            {
                var code = NewOtp6();
                var payload = ToJson(new
                {
                    code,
                    exp = DateTimeOffset.UtcNow.AddMinutes(10)
                });

                await _userManager.SetAuthenticationTokenAsync(user, Provider, OtpName, payload);

                var html = $@"
<p>Xin chào,</p>
<p>Mã OTP đặt lại mật khẩu của bạn là: <b style=""font-size:20px"">{code}</b></p>
<p>Mã có hiệu lực trong <b>10 phút</b>. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
<hr />
<p>HRTest</p>";
                await _email.SendAsync(model.Email, "[HRTest] Mã OTP đặt lại mật khẩu", html);
            }

            TempData["Info"] = "Nếu email tồn tại, mã OTP đã được gửi. Vui lòng kiểm tra hộp thư.";
            return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
        }

        // B2: Nhập OTP, sinh ResetPasswordToken (lưu tạm trong AspNetUserTokens)
        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(ForgotPassword));

            ViewData["Title"] = "Xác nhận OTP";
            return View(new ForgotPasswordVerifyViewModel { Email = email });
        }

        private record OtpPayload(string code, DateTimeOffset exp);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(ForgotPasswordVerifyViewModel model)
        {
            ViewData["Title"] = "Xác nhận OTP";
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại.");
                return View(model);
            }

            var tokenJson = await _userManager.GetAuthenticationTokenAsync(user, Provider, OtpName);
            var token = FromJson<OtpPayload>(tokenJson);
            if (token is null)
            {
                ModelState.AddModelError(string.Empty, "OTP không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            if (!string.Equals(token.code, model.Code))
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không đúng.");
                return View(model);
            }
            if (DateTimeOffset.UtcNow > token.exp)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn.");
                return View(model);
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.SetAuthenticationTokenAsync(user, Provider, ResetTokenName, resetToken);
            await _userManager.RemoveAuthenticationTokenAsync(user, Provider, OtpName); // không cho dùng lại

            return RedirectToAction(nameof(ResetPassword), new { email = model.Email });
        }

        // B3: Nhập mật khẩu mới, thực thi ResetPassword bằng token đã lưu
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(ForgotPassword));

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return RedirectToAction(nameof(ForgotPassword));

            var token = await _userManager.GetAuthenticationTokenAsync(user, Provider, ResetTokenName);
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Thiếu token đặt lại mật khẩu. Vui lòng thực hiện lại từ đầu.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            ViewData["Title"] = "Đặt mật khẩu mới";
            return View(new ForgotPasswordResetViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ForgotPasswordResetViewModel model)
        {
            ViewData["Title"] = "Đặt mật khẩu mới";
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["Error"] = "Email không tồn tại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View(model);
            }

            await _userManager.RemoveAuthenticationTokenAsync(user, Provider, ResetTokenName);

            TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Login));
        }
        #endregion
    }
}
