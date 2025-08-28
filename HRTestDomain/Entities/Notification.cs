namespace HRTestDomain.Entities
{
    public class Notification
    {
        public long Id { get; set; }
        public string? UserId { get; set; }       
        public string? RoleName { get; set; }      
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Url { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }

}
