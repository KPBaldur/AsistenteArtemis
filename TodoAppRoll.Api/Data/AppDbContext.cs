using Microsoft.EntityFrameworkCore;
using TodoAppRoll.Api.Models.Entities;
using Task = TodoAppRoll.Api.Models.Entities.Task;

namespace TodoAppRoll.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Task> Tasks { get; set; } = null!;
        public DbSet<Subtask> Subtasks { get; set; } = null!;
        public DbSet<Alarm> Alarms { get; set; } = null!;
        public DbSet<Media> MediaFiles { get; set; } = null!;
        public DbSet<AIChatSession> AIChatSessions { get; set; } = null!;
        public DbSet<AIMessage> AIMessages { get; set; } = null!;
        public DbSet<AISmartSuggestion> AISmartSuggestions { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuraciones adicionales (Fluent API) si son necesarias
            
            // Relación Task - Alarm (Opcional 1:1 o 1:N según lógica)
            modelBuilder.Entity<Task>()
                .HasOne(t => t.Alarm)
                .WithMany()
                .HasForeignKey(t => t.AlarmId)
                .OnDelete(DeleteBehavior.SetNull);

            // Alarmas con Multimedia (Audio e Imagen)
            modelBuilder.Entity<Alarm>()
                .HasOne(a => a.Audio)
                .WithMany()
                .HasForeignKey(a => a.AudioId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Alarm>()
                .HasOne(a => a.Image)
                .WithMany()
                .HasForeignKey(a => a.ImageId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
