using Microsoft.EntityFrameworkCore;

namespace Finance.Api.Data;

public class ExpenseEntity
{
    public long Seq { get; set; }              // keeps insertion order
    public Guid Id { get; set; }
    public string DepartmentCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public class BudgetEntity
{
    public string DepartmentCode { get; set; } = "";
    public decimal Allocated { get; set; }

    // BF-22: audit fields.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseEntity>(e =>
        {
            e.HasKey(x => x.Seq);
            e.Property(x => x.Seq).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Id).IsUnique();
            e.Property(x => x.DepartmentCode).HasMaxLength(20);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => x.DepartmentCode);
        });

        modelBuilder.Entity<BudgetEntity>(b =>
        {
            b.HasKey(x => x.DepartmentCode);
            b.Property(x => x.DepartmentCode).HasMaxLength(20);
            b.Property(x => x.Allocated).HasPrecision(18, 2);
        });
    }
}