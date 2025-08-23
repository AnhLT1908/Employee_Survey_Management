namespace HRTestWeb.Middlewares
{
    public class BlockRuleMiddleware
    {
        private readonly RequestDelegate _next;
        public BlockRuleMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();

            // Ví dụ: chặn IP nằm trong danh sách đen (đọc từ cấu hình)
            // if (IsBlacklisted(ip)) { ctx.Response.StatusCode = 403; return; }

            await _next(ctx);
        }
    }
}
