using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FileSharingandStorageSystem
{
    public class AppDBContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<FileMetaData> FileMetaData { get; set; }
        public DbSet<FileShare> FileShares { get; set; }

        // Backing store for ASP.NET Data Protection keys so they persist across
        // restarts/redeploys and are shared across instances (keeps auth cookies
        // and antiforgery tokens valid instead of being regenerated each boot).
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<FileMetaData>(entity =>
            {
                entity.Property(f => f.FileName).IsRequired().HasMaxLength(260);
                entity.Property(f => f.StoredFileName).IsRequired().HasMaxLength(80);
                entity.Property(f => f.FileType).HasMaxLength(255);
                entity.Property(f => f.OwnerId).IsRequired();
                entity.HasIndex(f => f.OwnerId);
            });

            builder.Entity<FileShare>(entity =>
            {
                entity.Property(s => s.Token).IsRequired().HasMaxLength(64);
                entity.Property(s => s.CreatedByUserId).IsRequired();
                entity.HasIndex(s => s.Token).IsUnique();

                entity.HasOne(s => s.File)
                    .WithMany()
                    .HasForeignKey(s => s.FileMetaDataId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
