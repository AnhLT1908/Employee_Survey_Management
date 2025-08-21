using System.Linq;
using System.Threading.Tasks;
using HRTestInfrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HRTestWeb.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public record LoginRequest(string Email, string Password, bool RememberMe, string ReturnUrl);
        public record LoginResponse(string UserName, string[] Roles, string RedirectUrl);

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Thiếu Email hoặc Mật khẩu." });

            var user = await _userManager.FindByEmailAsync(req.Email)
                       ?? await _userManager.FindByNameAsync(req.Email);

            if (user == null)
                return Unauthorized(new { message = "Tài khoản không tồn tại." });

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, req.Password, req.RememberMe, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    return Unauthorized(new { message = "Tài khoản tạm bị khoá. Vui lòng thử lại sau." });
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            var isAdmin = roles.Contains("Admin"); // có thể dùng await _userManager.IsInRoleAsync(user, "Admin")

            // ✅ Admin vào thẳng khu vực Admin (bỏ qua ReturnUrl)
            var redirect = isAdmin
                ? Url.Action("Index", "Home", new { area = "Admin" })!
                : (!string.IsNullOrEmpty(req.ReturnUrl) && Url.IsLocalUrl(req.ReturnUrl)
                    ? req.ReturnUrl
                    : Url.Action("Index", "Home")!);

            return Ok(new LoginResponse(user.UserName!, roles, redirect));
        }


        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Đã đăng xuất." });
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!User.Identity?.IsAuthenticated ?? true) return Ok(new { isAuthenticated = false });
            return Ok(new { isAuthenticated = true, name = User.Identity!.Name });
        }
    }
}
