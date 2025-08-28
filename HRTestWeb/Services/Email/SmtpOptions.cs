namespace HRTestWeb.Services.Email
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
        public string From { get; set; } = "HRTest <no-reply@local>";
    }
}
