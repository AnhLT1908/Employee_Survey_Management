namespace HRTestDomain.Entities
{
    public class Feedback
    {
        public int Id { get; set; }
        public int? AttemptId { get; set; }
        public int? TestId { get; set; }
        public string UserId { get; set; } = default!;
        public string Content { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
