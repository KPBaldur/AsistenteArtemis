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
        public DbSet<Note> Notes { get; set; } = null!;
        public DbSet<NoteLink> NoteLinks { get; set; } = null!;
        public DbSet<ServerMonitor> Monitors { get; set; } = null!;
        public DbSet<MonitorCheckResult> MonitorCheckResults { get; set; } = null!;
        public DbSet<AssistantIdentity> AssistantIdentities { get; set; } = null!;
        public DbSet<DeviceToken> DeviceTokens { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuraciones adicionales (Fluent API) si son necesarias

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

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

            // Nota - Tarea (asociación opcional, no cascadea al borrar la tarea)
            modelBuilder.Entity<Note>()
                .HasOne(n => n.Task)
                .WithMany()
                .HasForeignKey(n => n.TaskId)
                .OnDelete(DeleteBehavior.SetNull);

            // NoteLink: dos FKs a la misma tabla (Note) — Restrict en ambas para evitar
            // múltiples rutas de cascada; los links se limpian explícitamente al borrar/editar una nota.
            modelBuilder.Entity<NoteLink>()
                .HasOne(nl => nl.SourceNote)
                .WithMany(n => n.OutgoingLinks)
                .HasForeignKey(nl => nl.SourceNoteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NoteLink>()
                .HasOne(nl => nl.TargetNote)
                .WithMany(n => n.IncomingLinks)
                .HasForeignKey(nl => nl.TargetNoteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Historial de chequeos de un monitor: sin su padre no tiene sentido, sí cascadea.
            modelBuilder.Entity<MonitorCheckResult>()
                .HasOne(mcr => mcr.Monitor)
                .WithMany(m => m.CheckResults)
                .HasForeignKey(mcr => mcr.MonitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Una sola identidad activa por usuario.
            modelBuilder.Entity<AssistantIdentity>()
                .HasIndex(a => a.UserId)
                .IsUnique();

            // Tokens de dispositivo (mascota): se buscan por hash en cada request.
            modelBuilder.Entity<DeviceToken>()
                .HasIndex(d => d.TokenHash)
                .IsUnique();

            modelBuilder.Entity<DeviceToken>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
