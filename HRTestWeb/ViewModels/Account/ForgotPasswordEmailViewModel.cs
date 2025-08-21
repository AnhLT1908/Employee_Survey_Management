using System.ComponentModel.DataAnnotations;

namespace HRTestWeb.ViewModels.Account
{
    public class ForgotPasswordEmailViewModel
    {
        [Required, EmailAddress]
        [Display(Name = "Email công ty")]
        public string Email { get; set; } = string.Empty;
    }
}
