using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HRTestWeb.Hubs
{
    /// <summary>
    /// Hub gán connection vào các group theo user và theo role.
    /// Group role có format: "role:{RoleName}"
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // group theo user id (nếu cần gửi riêng từng người)
            var uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(uid))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");

            // group theo role
            var roles = Context.User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct() ?? Enumerable.Empty<string>();

            foreach (var r in roles)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{r}");

            await base.OnConnectedAsync();
        }
    }
}
