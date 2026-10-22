using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Domain;

namespace ShortLink.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Link>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => l.Code).IsUnique();
            entity.Property(l => l.Code).HasMaxLength(16).IsRequired();
            entity.Property(l => l.TargetUrl).HasMaxLength(2048).IsRequired();
        });
    }
}
