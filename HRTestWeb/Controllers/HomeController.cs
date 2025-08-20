using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRTestWeb.Controllers
{
    [Authorize]                 // <- bắt buộc đăng nhập mới vào Home
    public class HomeController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Privacy() => View();
    }
}
