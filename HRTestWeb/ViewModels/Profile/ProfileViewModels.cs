using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRTestWeb.ViewModels.Profile
{
    public class ProfilePageVM
    {
        public ProfileEditVM Profile { get; set; } = new();
        public ChangePasswordVM ChangePassword { get; set; } = new();
    }

    public class ProfileEditVM
    {
        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; } = "";

        [Required, EmailAddress, Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone, Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        // --- Chỉ hiển thị (read-only) ---
        [Display(Name = "Phòng ban")]
        public string? DepartmentDisplay { get; set; }  

        [Display(Name = "Level")]
        public string? LevelDisplay { get; set; } 

        [Display(Name = "Vai trò")]
        public List<string> Roles { get; set; } = new(); 
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string? CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải ít nhất 6 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string? ConfirmPassword { get; set; }
    }
}
