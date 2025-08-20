namespace HRTestDomain.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
    }
}
