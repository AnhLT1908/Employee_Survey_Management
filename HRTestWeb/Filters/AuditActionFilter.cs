using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace HRTestWeb.Filters
{
    public class AuditActionFilter : IAsyncActionFilter
    {
        private readonly ILogger<AuditActionFilter> _logger;
        private readonly HRTestWeb.Services.Logging.RequestLogFileWriter _writer;

        public AuditActionFilter(
            ILogger<AuditActionFilter> logger,
            HRTestWeb.Services.Logging.RequestLogFileWriter writer)
        {
            _logger = logger;
            _writer = writer;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var method = context.HttpContext.Request.Method.ToUpperInvariant();
            var shouldAudit = method is "POST" or "PUT" or "PATCH" or "DELETE";

            var user = context.HttpContext.User?.Identity?.IsAuthenticated == true
                ? context.HttpContext.User.Identity!.Name
                : "anonymous";

            var route = $"{context.RouteData.Values["area"]}/{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}"
                        .Replace("//", "/").Trim('/');

            var beforeArgs = shouldAudit
                ? JsonSerializer.Serialize(context.ActionArguments, new JsonSerializerOptions { WriteIndented = false })
                : null;

            var executed = await next(); // chạy action

            if (!shouldAudit) return;

            var status = executed.Exception == null ? "OK" : $"ERR:{executed.Exception.GetType().Name}";
            var modelErrors = context.ModelState.IsValid
                ? null
                : string.Join("; ", context.ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

            var record = $"[AUDIT] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | {route} | {method} | user={user} | status={status} | modelErrors={modelErrors ?? "-"} | args={beforeArgs}";
            _logger.LogInformation("{line}", record);
            await _writer.AppendLineAsync(record);

            // Nếu muốn ghi DB (bảng AuditLogs), bỏ comment dưới và chỉnh tên cột cho khớp schema của bạn.
            /*
            try
            {
                using var scope = context.HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HRTestInfrastructure.Data.HRTestDbContext>();
                db.AuditLogs.Add(new HRTestDomain.Entities.AuditLog
                {
                    // Điều chỉnh property đúng với entity của bạn
                    UserId = context.HttpContext.User?.FindFirst("sub")?.Value ?? user,
                    Action = $"{route} {method}",
                    Content = record,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch { /* fail-safe: log file vẫn có *-/ }
            */
        }
    }
}
