namespace HRTestDomain.Entities
{
    public class Test
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; } 
        public decimal PassScore { get; set; }
        public bool IsRandomized { get; set; } = false;
        public string? CreatedBy { get; set; }
    }
}
