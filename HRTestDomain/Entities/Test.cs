namespace HRTestDomain.Entities
{
    public class Test
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public decimal PassScore { get; set; } = 5m;
        public bool IsRandomized { get; set; } = false;
        public string? CreatedBy { get; set; }
    }
}
