namespace HRTestDomain.Entities
{
    public class QuestionBank
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }

        // NEW: gán kỹ năng cho cả ngân hàng câu hỏi
        public int? SkillId { get; set; }
        public Skill? Skill { get; set; }

    }
}
