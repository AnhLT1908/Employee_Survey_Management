namespace HRTestDomain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string ActorUserId { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string Entity { get; set; } = default!;
        public string? EntityId { get; set; }
        public string? DataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
