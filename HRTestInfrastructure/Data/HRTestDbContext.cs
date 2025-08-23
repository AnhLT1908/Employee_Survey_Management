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

            // -------- Precision cho các decimal (tránh truncate) --------
            modelBuilder.Entity<Question>().Property(p => p.Score).HasPrecision(10, 2);
            modelBuilder.Entity<Answer>().Property(p => p.Score).HasPrecision(10, 2);
            modelBuilder.Entity<Test>().Property(p => p.PassScore).HasPrecision(10, 2);
            modelBuilder.Entity<TestAttempt>().Property(p => p.TotalScore).HasPrecision(10, 2);

            // -------- QUAN HỆ (FK) GIỮA CÁC BẢNG --------

            // Question -> QuestionBank (n-N)
            modelBuilder.Entity<Question>()
                .HasOne<QuestionBank>()             // không cần navigation cũng được
                .WithMany()
                .HasForeignKey(q => q.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            // TestQuestion -> Test (n-1) & -> Question (n-1)
            modelBuilder.Entity<TestQuestion>()
                .HasOne<Test>()
                .WithMany()
                .HasForeignKey(tq => tq.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TestQuestion>()
                .HasOne<Question>()
                .WithMany()
                .HasForeignKey(tq => tq.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Assignment -> Test (n-1)
            modelBuilder.Entity<Assignment>()
                .HasOne<Test>()
                .WithMany()
                .HasForeignKey(a => a.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestAttempt -> Test (n-1)
            modelBuilder.Entity<TestAttempt>()
                .HasOne<Test>()
                .WithMany()
                .HasForeignKey(a => a.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestAttempt -> AspNetUsers (n-1)
            modelBuilder.Entity<TestAttempt>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Answer -> TestAttempt (n-1) & -> Question (n-1)
            modelBuilder.Entity<Answer>()
                .HasOne<TestAttempt>()
                .WithMany()
                .HasForeignKey(a => a.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasOne<Question>()
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Feedback -> TestAttempt (0..1-n)  &  -> Test (0..1-n)  &  -> AspNetUsers (n-1)
            modelBuilder.Entity<Feedback>()
                .HasOne<TestAttempt>()
                .WithMany()
                .HasForeignKey(f => f.AttemptId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Feedback>()
      .HasOne<Test>()
      .WithMany()
      .HasForeignKey(f => f.TestId)
      .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification -> AspNetUsers (n-1)
            modelBuilder.Entity<Notification>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditLog -> AspNetUsers (n-1)
            modelBuilder.Entity<AuditLog>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ApplicationUser -> Department (0..1-n)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne<Department>()
                .WithMany()
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);


            modelBuilder.Entity<ApplicationUser>()
    .HasOne(u => u.Level)
    .WithMany()
    .HasForeignKey(u => u.LevelId)
    .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Level>()
                .HasIndex(x => x.Name).IsUnique();

            // Seed sẵn các level
            modelBuilder.Entity<Level>().HasData(
                new Level { Id = 1, Name = "Intern" },
                new Level { Id = 2, Name = "Fresher" },
                new Level { Id = 3, Name = "Junior" },
                new Level { Id = 4, Name = "Middle" },
                new Level { Id = 5, Name = "Senior" },
                new Level { Id = 6, Name = "Lead" },
                new Level { Id = 7, Name = "Principal" },
                new Level { Id = 8, Name = "Manager" }
            );

            // -------- Indexes hữu ích --------
            modelBuilder.Entity<TestAttempt>().HasIndex(x => new { x.TestId, x.UserId });
            modelBuilder.Entity<Answer>().HasIndex(x => new { x.AttemptId, x.QuestionId });
            modelBuilder.Entity<Assignment>().HasIndex(x => new { x.TestId, x.TargetType, x.TargetValue });
            modelBuilder.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        }
    }
}
