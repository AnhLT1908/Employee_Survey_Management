using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRTestWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]       // Chỉ Admin mới vào được
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
    }
}
