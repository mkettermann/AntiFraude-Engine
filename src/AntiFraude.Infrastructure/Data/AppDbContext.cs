using AntiFraude.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiFraude.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Transaction ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TransactionId).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.TransactionId).IsUnique();
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Currency).HasMaxLength(10);
            e.Property(t => t.MerchantId).IsRequired().HasMaxLength(100);
            e.Property(t => t.CustomerId).IsRequired().HasMaxLength(100);
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.Decision).HasConversion<string>();
            e.Property(t => t.Metadata)
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null));
        });

        // ── AuditLog ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.FromStatus).HasConversion<string>();
            e.Property(a => a.ToStatus).HasConversion<string>();
            e.Property(a => a.Message).HasMaxLength(500);
            e.HasIndex(a => a.TransactionId);
        });

        // ── ProcessedMessage (idempotência) ───────────────────────────────────────
        modelBuilder.Entity<ProcessedMessage>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.IdempotencyKeyHash).IsRequired().HasMaxLength(64);
            // Constraint UNIQUE garante que a mesma chave nunca seja processada duas vezes,
            // mesmo em condições de race condition.
            e.HasIndex(p => p.IdempotencyKeyHash).IsUnique();
            e.Property(p => p.TransactionId).IsRequired().HasMaxLength(100);
        });

        // ── OutboxMessage (Outbox Pattern) ────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.MessageType).IsRequired().HasMaxLength(200);
            e.Property(o => o.Payload).IsRequired();
            e.HasIndex(o => new { o.ProcessedAt, o.CreatedAt });
        });
    }
}
