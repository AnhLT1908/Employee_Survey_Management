namespace HRTestDomain.Entities
{
    public class Skill
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;      // ví dụ: C#, SQL, QA, Communication
        public string? Description { get; set; }

        // Navigation: 1 Skill -> nhiều QuestionBanks
        public ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();
    }
}
