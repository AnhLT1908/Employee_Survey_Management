using System.ComponentModel.DataAnnotations;

namespace HRTestDomain.Entities
{
    public class AppSetting
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Key { get; set; } = default!;

        public string? Value { get; set; }
    }
}
