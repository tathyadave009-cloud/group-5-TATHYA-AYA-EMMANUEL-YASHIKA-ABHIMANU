using Finance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Finance.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Revenue> Revenues => Set<Revenue>();
}