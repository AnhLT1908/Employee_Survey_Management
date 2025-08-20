using Microsoft.AspNetCore.Identity;

namespace HRTestInfrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public int? DepartmentId { get; set; } 
        public string? Level { get; set; }  
    }
}
