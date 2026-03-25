using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;

namespace TransactionsIngest.Data;

public class AppDbContext : DbContext
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionRevision> TransactionRevisions { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.TransactionId);
            entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            entity.Property(t => t.CardNumberHash).HasMaxLength(64);
            entity.Property(t => t.CardNumberLast4).HasMaxLength(4);
            entity.Property(t => t.LocationCode).HasMaxLength(20);
            entity.Property(t => t.ProductName).HasMaxLength(20);
            entity.Property(t => t.Status).HasMaxLength(20);
        });
    }
}
