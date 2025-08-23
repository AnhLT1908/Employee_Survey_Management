using System.Text;

namespace HRTestWeb.Services.Logging
{
    // Ghi log ra file theo ngày: wwwroot/_logs/requests-YYYY-MM-DD.log
    public class RequestLogFileWriter
    {
        private readonly IWebHostEnvironment _env;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public RequestLogFileWriter(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task AppendLineAsync(string line)
        {
            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "_logs");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"requests-{DateTime.UtcNow:yyyy-MM-dd}.log");
            await _gate.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(file, line + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
