using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HRTestInfrastructure.Identity;
using HRTestWeb.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRTestWeb.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtOptions _jwt;
        private readonly IWebHostEnvironment _env;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IOptions<JwtOptions> jwt,
            IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _jwt = jwt.Value;
            _env = env;
        }

        public record LoginRequest(string Email, string Password, bool RememberMe, string? ReturnUrl);
        public record LoginResponse(string UserName, string[] Roles, string RedirectUrl, string? Token);

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Thiếu Email hoặc Mật khẩu." });

            // Cho phép login bằng email hoặc username
            var user = await _userManager.FindByEmailAsync(req.Email)
                       ?? await _userManager.FindByNameAsync(req.Email);
            if (user == null)
                return Unauthorized(new { message = "Tài khoản không tồn tại." });

            var result = await _signInManager.PasswordSignInAsync(user, req.Password, req.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    return Unauthorized(new { message = "Tài khoản tạm bị khoá. Vui lòng thử lại sau." });
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
            }

            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            var isAdmin = roles.Contains("Admin");

            // === Issue JWT 30 phút ===
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds);
            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            // Lưu vào cookie HttpOnly để API đọc tự động
            Response.Cookies.Append("auth_token", tokenStr, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30)
            });

            // Redirect đích
            var homeUrl = Url.Action("Index", "Home") ?? "/";
            var adminUrl = Url.Action("Index", "Home", new { area = "Admin" }) ?? "/Admin/Home/Index";
            var redirect = isAdmin
                ? adminUrl
                : (!string.IsNullOrEmpty(req.ReturnUrl) && Url.IsLocalUrl(req.ReturnUrl) ? req.ReturnUrl : homeUrl);

            var nameForClient = user.UserName ?? user.Email ?? user.Id;

            // Trả token về chỉ khi môi trường Development (để bạn console.log kiểm tra)
            var tokenForClient = _env.IsDevelopment() ? tokenStr : null;

            return Ok(new LoginResponse(nameForClient, roles, redirect, tokenForClient));
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            Response.Cookies.Delete("auth_token");
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
