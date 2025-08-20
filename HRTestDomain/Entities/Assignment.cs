namespace HRTestDomain.Entities
{
    public class Assignment
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public string TargetType { get; set; } = "Department";   // Department/Role/Level/User
        public string TargetValue { get; set; } = default!;
        public DateTime StartAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
