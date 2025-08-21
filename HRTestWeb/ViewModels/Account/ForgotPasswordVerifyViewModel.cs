using System.ComponentModel.DataAnnotations;

namespace HRTestWeb.ViewModels.Account
{
    public class ForgotPasswordVerifyViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(6, MinimumLength = 6)]
        [Display(Name = "Mã OTP")]
        public string Code { get; set; } = string.Empty;
    }
}
