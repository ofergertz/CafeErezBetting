using System.Text.Json;
using CafeErezBetting.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Expression trees cannot use optional args — wrap in static helper
    private static JsonDocument ParseJson(string json) => JsonDocument.Parse(json);
    public DbSet<Customer>    Customers    => Set<Customer>();
    public DbSet<DebtRecord>  DebtRecords  => Set<DebtRecord>();
    public DbSet<BettingForm> BettingForms => Set<BettingForm>();
    public DbSet<WinnerMatch> WinnerMatches => Set<WinnerMatch>();
    public DbSet<Product>     Products     => Set<Product>();
    public DbSet<AuditLog>    AuditLogs    => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Phone).IsUnique();
            e.HasIndex(c => c.IdNumber).IsUnique();
            e.Property(c => c.FirstName).HasMaxLength(100);
            e.Property(c => c.LastName).HasMaxLength(100);
            e.Property(c => c.IdNumber).HasMaxLength(9);
            e.Property(c => c.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<DebtRecord>(e =>
        {
            e.Property(d => d.OriginalAmount).HasPrecision(18, 2);
            e.Property(d => d.PaidAmount).HasPrecision(18, 2);
            e.Ignore(d => d.Balance); // computed
            e.HasOne(d => d.Customer)
             .WithMany(c => c.Debts)
             .HasForeignKey(d => d.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BettingForm>(e =>
        {
            e.Property(f => f.Payload)
             .HasColumnType("jsonb")
             .HasConversion(
                 v => v.RootElement.GetRawText(),
                 v => ParseJson(v)
             );
            e.HasOne(f => f.Customer)
             .WithMany(c => c.Forms)
             .HasForeignKey(f => f.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Payload)
             .HasColumnType("jsonb")
             .HasConversion(
                 v => v.RootElement.GetRawText(),
                 v => ParseJson(v)
             );
            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.Role).HasMaxLength(20);
        });

        modelBuilder.Entity<WinnerMatch>(e =>
        {
            e.HasIndex(m => m.ExternalId).IsUnique();
            e.Property(m => m.Odds1).HasPrecision(8, 2);
            e.Property(m => m.OddsX).HasPrecision(8, 2);
            e.Property(m => m.Odds2).HasPrecision(8, 2);
        });
    }
}
