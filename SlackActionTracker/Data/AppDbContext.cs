using Microsoft.EntityFrameworkCore;
using SlackActionTracker.Domain;

namespace SlackActionTracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ActionItem> ActionItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActionItem>()
            .HasIndex(a => a.SlackEventId)
            .IsUnique();
    }
}