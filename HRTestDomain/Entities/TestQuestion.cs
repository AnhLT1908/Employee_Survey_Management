namespace HRTestDomain.Entities
{
    public class TestQuestion
    {
        public int Id { get; set; }              // dùng Id tự tăng cho đơn giản
        public int TestId { get; set; }
        public int QuestionId { get; set; }
        public int Order { get; set; }
    }
}
