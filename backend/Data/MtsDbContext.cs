using backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class MtsDbContext(DbContextOptions<MtsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Chemical> Chemicals => Set<Chemical>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<RelocationTask> RelocationTasks => Set<RelocationTask>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Roles & Users (Many-to-Many via user_roles)
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).IsRequired().HasMaxLength(100);
            e.Property(u => u.Password).IsRequired().HasMaxLength(255);
            e.Property(u => u.AuthSource).HasColumnName("auth_source").HasMaxLength(20);
            e.Property(u => u.Active).HasColumnName("active").HasDefaultValue(true);

            e.HasMany(u => u.Roles)
             .WithMany(r => r.Users)
             .UsingEntity(j => j.ToTable("user_roles"));

            e.HasMany(u => u.RefreshTokens)
             .WithOne(t => t.User)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UserId, t.ExpiresAt });
            e.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
        });

        // 2. Chemicals
        modelBuilder.Entity<Chemical>(e =>
        {
            e.ToTable("chemicals");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.ChemicalCode).HasColumnName("chemical_code").IsRequired().HasMaxLength(50);
            e.HasIndex(c => c.ChemicalCode).IsUnique();
            e.Property(c => c.Description).HasColumnName("description");
            e.Property(c => c.MsdsUrl).HasColumnName("msds_url").HasMaxLength(500);
            e.Property(c => c.Stopped).HasColumnName("stopped").HasDefaultValue(false);
            e.Property(c => c.StorageType).HasColumnName("storage_type").IsRequired().HasMaxLength(30).HasDefaultValue("GENERAL");
        });

        // 3. Batches
        modelBuilder.Entity<Batch>(e =>
        {
            e.ToTable("batches");
            e.HasKey(b => b.Id);
            e.Property(b => b.BatchNo).HasColumnName("batch_no").IsRequired().HasMaxLength(100);
            e.HasIndex(b => b.BatchNo).IsUnique();
            e.Property(b => b.Supplier).HasColumnName("supplier").HasMaxLength(200);
            e.Property(b => b.InitialQuantity).HasColumnName("initial_quantity");
            e.Property(b => b.ExpirationDate).HasColumnName("expiration_date");
            e.Property(b => b.ChemicalId).HasColumnName("chemical_id");

            e.HasOne(b => b.Chemical)
             .WithMany(c => c.Batches)
             .HasForeignKey(b => b.ChemicalId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Addresses
        modelBuilder.Entity<Address>(e =>
        {
            e.ToTable("addresses");
            e.HasKey(a => a.Id);
            e.Property(a => a.Code).HasColumnName("code").IsRequired().HasMaxLength(50);
            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.MaxCapacity).HasColumnName("max_capacity").IsRequired();
            e.Property(a => a.StorageType).HasColumnName("storage_type").IsRequired().HasMaxLength(30).HasDefaultValue("GENERAL");
        });

        // 5. Units
        modelBuilder.Entity<Unit>(e =>
        {
            e.ToTable("units");
            e.HasKey(u => u.Id);
            e.Property(u => u.Barcode).HasColumnName("barcode").IsRequired().HasMaxLength(100);
            e.HasIndex(u => u.Barcode).IsUnique();
            e.Property(u => u.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(u => u.ConsumedAt).HasColumnName("consumed_at");
            e.Property(u => u.BatchId).HasColumnName("batch_id");
            e.Property(u => u.AddressId).HasColumnName("address_id");
            e.Property(u => u.Version).HasColumnName("version").IsConcurrencyToken();

            e.HasOne(u => u.Batch)
             .WithMany(b => b.Units)
             .HasForeignKey(u => u.BatchId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.Address)
             .WithMany(a => a.Units)
             .HasForeignKey(u => u.AddressId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // 6. RelocationTasks
        modelBuilder.Entity<RelocationTask>(e =>
        {
            e.ToTable("relocation_tasks");
            e.HasKey(t => t.Id);
            e.Property(t => t.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.Property(t => t.CompletedAt).HasColumnName("completed_at");
            e.Property(t => t.UnitId).HasColumnName("unit_id");
            e.Property(t => t.FromAddressId).HasColumnName("from_address_id");
            e.Property(t => t.ToAddressId).HasColumnName("to_address_id");
            e.Property(t => t.CompletedByUserId).HasColumnName("completed_by_user_id");
            e.HasIndex(t => t.UnitId)
             .IsUnique()
             .HasFilter("status = 'Pending'");
            e.HasIndex(t => new { t.Status, t.CreatedAt });

            e.HasOne(t => t.Unit).WithMany().HasForeignKey(t => t.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.FromAddress).WithMany().HasForeignKey(t => t.FromAddressId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToAddress).WithMany().HasForeignKey(t => t.ToAddressId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CompletedByUser).WithMany().HasForeignKey(t => t.CompletedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Unit>(e =>
        {
            e.HasIndex(u => new { u.Status, u.BatchId });
            e.HasIndex(u => new { u.AddressId, u.Status });
        });

        modelBuilder.Entity<Batch>(e =>
        {
            e.HasIndex(b => new { b.ChemicalId, b.ExpirationDate });
        });
    }
}
