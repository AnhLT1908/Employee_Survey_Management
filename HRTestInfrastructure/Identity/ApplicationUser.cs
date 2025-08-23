using HRTestDomain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HRTestInfrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public int? DepartmentId { get; set; }  
        // public string? Level { get; set; }   

        public int? LevelId { get; set; }    
        public Level? Level { get; set; }
    }
}
