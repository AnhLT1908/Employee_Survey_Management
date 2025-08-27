using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HRTestWeb.Services.Settings
{
    public class AppSettingsDto
    {
        public string SiteName { get; set; } = "HRTest";
        public string? LogoPath { get; set; } = "/img/logo_black.png";

        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public string From { get; set; } = "HRTest <no-reply@local>";
        public bool UseStartTls { get; set; } = true;
    }

    public interface IAppSettingsService
    {
        Task<AppSettingsDto> GetCachedAsync();
        Task UpdateAsync(AppSettingsDto dto);
        Task<string?> SaveLogoAsync(IFormFile file);
    }
}
