namespace HRTestDomain.Entities
{
    public class Skill
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }

        // navigation
        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
