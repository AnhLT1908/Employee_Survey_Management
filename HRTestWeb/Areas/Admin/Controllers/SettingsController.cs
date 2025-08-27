using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using HRTestWeb.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly IAppSettingsService _svc;
        public SettingsController(IAppSettingsService svc) => _svc = svc;

        public class SettingsVM
        {
            [Required, Display(Name = "Tên hệ thống")]
            public string SiteName { get; set; } = "HRTest";

            public string? CurrentLogoPath { get; set; }

            [Display(Name = "Logo mới (tuỳ chọn)")]
            public IFormFile? LogoFile { get; set; }

            [Display(Name = "SMTP Host")] public string? SmtpHost { get; set; }
            [Display(Name = "SMTP Port")] public int SmtpPort { get; set; } = 587;
            [Display(Name = "SMTP User")] public string? SmtpUser { get; set; }
            [Display(Name = "SMTP Pass")] public string? SmtpPass { get; set; }
            [Display(Name = "Email From")] public string? From { get; set; }
            [Display(Name = "Dùng STARTTLS")] public bool UseStartTls { get; set; } = true;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var s = await _svc.GetCachedAsync();
            return View(new SettingsVM
            {
                SiteName = s.SiteName,
                CurrentLogoPath = s.LogoPath,
                SmtpHost = s.SmtpHost,
                SmtpPort = s.SmtpPort,
                SmtpUser = s.SmtpUser,
                SmtpPass = s.SmtpPass,
                From = s.From,
                UseStartTls = s.UseStartTls
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.LogoFile != null)
            {
                var path = await _svc.SaveLogoAsync(vm.LogoFile);
                if (!string.IsNullOrWhiteSpace(path)) vm.CurrentLogoPath = path;
            }

            await _svc.UpdateAsync(new AppSettingsDto
            {
                SiteName = vm.SiteName.Trim(),
                LogoPath = vm.CurrentLogoPath,
                SmtpHost = vm.SmtpHost ?? "",
                SmtpPort = vm.SmtpPort,
                SmtpUser = vm.SmtpUser ?? "",
                SmtpPass = vm.SmtpPass ?? "",
                From = vm.From ?? "",
                UseStartTls = vm.UseStartTls
            });

            TempData["Success"] = "Đã lưu cấu hình hệ thống.";
            return RedirectToAction(nameof(Index));
        }
    }
}
