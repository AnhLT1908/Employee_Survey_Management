namespace HRTestDomain.Entities
{
    public class Answer
    {
        public int Id { get; set; }
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string? AnswerJson { get; set; }
        public decimal? Score { get; set; }
        public string? GradedBy { get; set; }
        public DateTime? GradedAt { get; set; }
    }
}
