namespace HRTestDomain.Entities
{
    public class TestAttempt
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public string UserId { get; set; } = default!; // Identity user id (string)
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public string Status { get; set; } = "Draft";  // Draft/Submitted/Graded
        public decimal? TotalScore { get; set; }
    }
}
