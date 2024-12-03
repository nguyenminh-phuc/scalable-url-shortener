using Microsoft.EntityFrameworkCore;

namespace Shortener.Admin.Data;

public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<BannedDomain> BannedDomains { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BannedDomain>().ToTable("BannedDomain");
        modelBuilder.Entity<BannedDomain>().HasKey(x => x.Id);
        modelBuilder.Entity<BannedDomain>().Property(x => x.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<BannedDomain>().Property(x => x.Name).IsRequired();
        modelBuilder.Entity<BannedDomain>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<BannedDomain>().Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
