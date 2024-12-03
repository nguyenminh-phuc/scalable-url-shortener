using Microsoft.EntityFrameworkCore;

namespace Shortener.GrpcBackend.Data;

public sealed class BackendDbContext(DbContextOptions<BackendDbContext> options) : DbContext(options)
{
    public DbSet<Domain> Domains { get; init; }

    public DbSet<Range> Ranges { get; init; }

    public DbSet<User> Users { get; init; }

    public DbSet<Url> Urls { get; init; }

    public DbSet<Visit> Visits { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain>().ToTable("Domain");
        modelBuilder.Entity<Domain>().HasKey(x => x.Id);
        modelBuilder.Entity<Domain>().Property(x => x.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<Domain>().Property(x => x.Name).IsRequired();
        modelBuilder.Entity<Domain>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Domain>().Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        modelBuilder.Entity<Range>().ToTable("Range");
        modelBuilder.Entity<Range>().HasKey(x => x.Id);
        modelBuilder.Entity<Range>().Property(x => x.RangeId).IsRequired();
        modelBuilder.Entity<Range>().HasIndex(x => x.RangeId).IsUnique();

        modelBuilder.Entity<User>().ToTable("User");
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().Property(x => x.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<User>().Property(x => x.Username).IsRequired();
        modelBuilder.Entity<User>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<User>().Property(x => x.HashedPassword).IsRequired();
        modelBuilder.Entity<User>().Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<User>().Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        modelBuilder.Entity<Url>().ToTable("Url");
        modelBuilder.Entity<Url>().HasKey(x => x.Id);
        modelBuilder.Entity<Url>().Property(x => x.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<Url>().Property(x => x.DestinationUrl).IsRequired();
        modelBuilder.Entity<Url>().Property(x => x.TotalViews).HasDefaultValue(0);
        modelBuilder.Entity<Url>().Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<Url>().Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<Url>()
            .HasOne(x => x.User)
            .WithMany(x => x.Urls)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Url>()
            .HasOne(x => x.Domain)
            .WithMany(x => x.Urls)
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Visit>().ToTable("Visit");
        modelBuilder.Entity<Visit>().HasKey(x => x.Id);
        modelBuilder.Entity<Visit>().Property(x => x.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<Visit>().Property(x => x.Total).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.BrowserType).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.RobotType).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.UnknownType).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Platforms).HasColumnType("jsonb").IsRequired();
        modelBuilder.Entity<Visit>().Property(x => x.Windows).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Linux).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Ios).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.MacOs).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Android).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.OtherPlatform).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(e => e.Browsers).HasColumnType("jsonb");
        modelBuilder.Entity<Visit>().Property(x => x.Chrome).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Edge).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Firefox).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.InternetExplorer).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Opera).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.Safari).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(x => x.OtherBrowser).HasDefaultValue(0);
        modelBuilder.Entity<Visit>().Property(e => e.MobileDeviceTypes).HasColumnType("jsonb");
        modelBuilder.Entity<Visit>().Property(e => e.Countries).HasColumnType("jsonb");
        modelBuilder.Entity<Visit>().Property(e => e.Referrers).HasColumnType("jsonb");
        modelBuilder.Entity<Visit>().Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<Visit>().Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<Visit>()
            .HasOne(x => x.Url)
            .WithMany(x => x.Visits)
            .HasForeignKey(x => x.UrlId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
