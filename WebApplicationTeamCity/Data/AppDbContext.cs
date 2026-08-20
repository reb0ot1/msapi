using Microsoft.EntityFrameworkCore;
using WebApplicationTeamCity.Models;

namespace WebApplicationTeamCity.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });
    }
}
