using System;
using System.IO;
using System.Threading.Tasks;
using HRTestDomain.Entities;
using HRTestInfrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HRTestWeb.Services.Settings
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly HRTestDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private const string CACHEKEY = "APP_SETTINGS_CACHE";

        public AppSettingsService(HRTestDbContext db, IMemoryCache cache, IWebHostEnvironment env)
        {
            _db = db; _cache = cache; _env = env;
        }

        public async Task<AppSettingsDto> GetCachedAsync()
        {
            if (_cache.TryGetValue(CACHEKEY, out AppSettingsDto dto))
                return dto!;

            dto = new AppSettingsDto
            {
                SiteName = await Get("SiteName", "HRTest"),
                LogoPath = await Get("LogoPath", "/img/logo_black.png"),
                SmtpHost = await Get("Smtp.Host", ""),
                SmtpPort = int.TryParse(await Get("Smtp.Port", "587"), out var p) ? p : 587,
                SmtpUser = await Get("Smtp.User", ""),
                SmtpPass = await Get("Smtp.Pass", ""),
                From = await Get("Smtp.From", "HRTest <no-reply@local>"),
                UseStartTls = (await Get("Smtp.UseStartTls", "true")).Equals("true", StringComparison.OrdinalIgnoreCase)
            };

            _cache.Set(CACHEKEY, dto, TimeSpan.FromMinutes(5));
            return dto;
        }

        public async Task UpdateAsync(AppSettingsDto d)
        {
            await Upsert("SiteName", d.SiteName);
            await Upsert("LogoPath", d.LogoPath);
            await Upsert("Smtp.Host", d.SmtpHost);
            await Upsert("Smtp.Port", d.SmtpPort.ToString());
            await Upsert("Smtp.User", d.SmtpUser);
            await Upsert("Smtp.Pass", d.SmtpPass);
            await Upsert("Smtp.From", d.From);
            await Upsert("Smtp.UseStartTls", d.UseStartTls ? "true" : "false");
            await _db.SaveChangesAsync();
            _cache.Remove(CACHEKEY);
        }

        public async Task<string?> SaveLogoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);

            var ext = Path.GetExtension(file.FileName);
            var fname = $"logo_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
            var full = Path.Combine(uploads, fname);

            using var fs = File.Create(full);
            await file.CopyToAsync(fs);

            return "/uploads/" + fname; // relative URL
        }

        private async Task<string> Get(string key, string defaultValue)
        {
            var e = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
            return e?.Value ?? defaultValue;
        }

        private async Task Upsert(string key, string? value)
        {
            var e = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (e == null) _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else e.Value = value;
        }
    }
}
