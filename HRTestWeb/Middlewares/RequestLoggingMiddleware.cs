using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HRTestWeb.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly Services.Logging.RequestLogFileWriter _writer;

        // Các đuôi tĩnh không cần log (tuỳ biến)
        private static readonly string[] _staticExt = [".css", ".js", ".png", ".jpg", ".svg", ".ico", ".woff", ".woff2", ".map"];

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger,
            Services.Logging.RequestLogFileWriter writer)
        {
            _next = next;
            _logger = logger;
            _writer = writer;
        }

        public async Task Invoke(HttpContext ctx)
        {
            var path = ctx.Request.Path.Value ?? "/";
            if (_staticExt.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(ctx);
                return;
            }

            var sw = Stopwatch.StartNew();

            // Correlation-Id
            var cid = ctx.Request.Headers.TryGetValue("X-Request-ID", out var hv) && !string.IsNullOrWhiteSpace(hv)
                ? hv.ToString()
                : Guid.NewGuid().ToString("n");
            ctx.Response.Headers["X-Request-ID"] = cid;

            await _next(ctx);

            sw.Stop();

            var user = ctx.User?.Identity?.IsAuthenticated == true ? ctx.User.Identity!.Name : "anonymous";
            var ip = ctx.Connection?.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();
            var msg = new StringBuilder()
                .Append("[REQ] ")
                .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append(" | ").Append(ctx.Request.Method)
                .Append(" ").Append(path)
                .Append(ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : "")
                .Append(" | ").Append("status=").Append(ctx.Response?.StatusCode)
                .Append(" | ").Append("ms=").Append(sw.ElapsedMilliseconds)
                .Append(" | ").Append("user=").Append(user)
                .Append(" | ").Append("ip=").Append(ip)
                .Append(" | ").Append("cid=").Append(cid)
                .Append(" | ").Append("ua=").Append(ua);

            _logger.LogInformation("{line}", msg.ToString());
            await _writer.AppendLineAsync(msg.ToString());
        }
    }
}
