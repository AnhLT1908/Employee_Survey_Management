namespace HRTestDomain.Entities
{
    public class Question
    {
        public int Id { get; set; }
        public int BankId { get; set; }
        public string Content { get; set; } = default!;
        public int Type { get; set; }            // 0 MCQ, 1 Essay, 2 TrueFalse, 3 DragDrop, 4 Matching
        public int Difficulty { get; set; }      // 0 Junior, 1 Middle, 2 Senior
        public string? OptionsJson { get; set; }
        public string? CorrectAnswerJson { get; set; }
        public decimal Score { get; set; } = 1m;
    }
}
