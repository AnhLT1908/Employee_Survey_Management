using HRTestDomain.Entities;
using HRTestInfrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRTestInfrastructure.Data
{
    public class HRTestDbContext : IdentityDbContext<ApplicationUser>
    {
        public HRTestDbContext(DbContextOptions<HRTestDbContext> options) : base(options) { }

        public DbSet<Level> Levels => Set<Level>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Skill> Skills => Set<Skill>();                 // <-- NEW
        public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<Test> Tests => Set<Test>();
        public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
        public DbSet<Assignment> Assignments => Set<Assignment>();
        public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
        public DbSet<Answer> Answers => Set<Answer>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // -------- Precision --------
            modelBuilder.Entity<Question>().Property(p => p.Score).HasPrecision(10, 2);
            modelBuilder.Entity<Answer>().Property(p => p.Score).HasPrecision(10, 2);
            modelBuilder.Entity<Test>().Property(p => p.PassScore).HasPrecision(10, 2);
            modelBuilder.Entity<TestAttempt>().Property(p => p.TotalScore).HasPrecision(10, 2);

            // -------- Quan hệ --------

            // Question -> QuestionBank (n-1)
            modelBuilder.Entity<Question>()
                .HasOne<QuestionBank>()
                .WithMany()
                .HasForeignKey(q => q.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: QuestionBank -> Skill (n-1, optional)
            modelBuilder.Entity<QuestionBank>()
                .HasOne(qb => qb.Skill)
                .WithMany(s => s.QuestionBanks)
                .HasForeignKey(qb => qb.SkillId)
                .OnDelete(DeleteBehavior.SetNull);

            // TestQuestion -> Test & Question
            modelBuilder.Entity<TestQuestion>()
                .HasOne<Test>().WithMany()
                .HasForeignKey(tq => tq.TestId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TestQuestion>()
                .HasOne<Question>().WithMany()
                .HasForeignKey(tq => tq.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Assignment -> Test
            modelBuilder.Entity<Assignment>()
                .HasOne<Test>().WithMany()
                .HasForeignKey(a => a.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestAttempt -> Test
            modelBuilder.Entity<TestAttempt>()
                .HasOne<Test>().WithMany()
                .HasForeignKey(a => a.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestAttempt -> AspNetUsers
            modelBuilder.Entity<TestAttempt>()
                .HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Answer -> Attempt & Question
            modelBuilder.Entity<Answer>()
                .HasOne<TestAttempt>().WithMany()
                .HasForeignKey(a => a.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Answer>()
                .HasOne<Question>().WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Feedback -> Attempt/Test/User
            modelBuilder.Entity<Feedback>()
                .HasOne<TestAttempt>().WithMany()
                .HasForeignKey(f => f.AttemptId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Feedback>()
                .HasOne<Test>().WithMany()
                .HasForeignKey(f => f.TestId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Feedback>()
                .HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification -> User
            modelBuilder.Entity<Notification>()
                .HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditLog -> User
            modelBuilder.Entity<AuditLog>()
                .HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Department, Level
            modelBuilder.Entity<ApplicationUser>()
                .HasOne<Department>().WithMany()
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Level).WithMany()
                .HasForeignKey(u => u.LevelId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index
            modelBuilder.Entity<Level>().HasIndex(x => x.Name).IsUnique();
            modelBuilder.Entity<Skill>().HasIndex(x => x.Name).IsUnique(); // <-- NEW

            // (Tuỳ chọn) seed một vài skill cơ bản
            modelBuilder.Entity<Skill>().HasData(
                new Skill { Id = 1, Name = "C#", Description = "Ngôn ngữ C#" },
                new Skill { Id = 2, Name = "SQL", Description = "Cơ sở dữ liệu & truy vấn" },
                new Skill { Id = 3, Name = "QA", Description = "Kiểm thử phần mềm" }
            );

            // Indexes hữu ích
            modelBuilder.Entity<TestAttempt>().HasIndex(x => new { x.TestId, x.UserId });
            modelBuilder.Entity<Answer>().HasIndex(x => new { x.AttemptId, x.QuestionId });
            modelBuilder.Entity<Assignment>().HasIndex(x => new { x.TestId, x.TargetType, x.TargetValue });
            modelBuilder.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        }
    }
}
