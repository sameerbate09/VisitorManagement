using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Data;
using VisitorManagement.Domain.Constants;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.Name).IsUnique();

        b.HasData(
            new Role { Id = 1, Name = RoleNames.Admin },
            new Role { Id = 2, Name = RoleNames.User });
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).IsRequired().HasMaxLength(50);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.HasIndex(x => x.Username).IsUnique();

        b.HasOne(x => x.Role)
         .WithMany(r => r.Users)
         .HasForeignKey(x => x.RoleId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HostConfiguration : IEntityTypeConfiguration<Host>
{
    public void Configure(EntityTypeBuilder<Host> b)
    {
        b.ToTable("Hosts");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).IsRequired().HasMaxLength(254);
        b.Property(x => x.Department).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.FullName);   // supports type-ahead search

        b.HasData(
            new Host { Id = 1, FullName = "Aarav Sharma", Email = "aarav.sharma@example.com", Department = "Engineering", IsActive = true },
            new Host { Id = 2, FullName = "Priya Nair", Email = "priya.nair@example.com", Department = "Human Resources", IsActive = true },
            new Host { Id = 3, FullName = "Rohan Mehta", Email = "rohan.mehta@example.com", Department = "Finance", IsActive = true },
            new Host { Id = 4, FullName = "Sneha Kulkarni", Email = "sneha.kulkarni@example.com", Department = "Operations", IsActive = true },
            new Host { Id = 5, FullName = "Vikram Singh", Email = "vikram.singh@example.com", Department = "Sales", IsActive = true },
            new Host { Id = 6, FullName = "Ananya Iyer", Email = "ananya.iyer@example.com", Department = "Engineering", IsActive = true },
            new Host { Id = 7, FullName = "Karan Patel", Email = "karan.patel@example.com", Department = "Legal", IsActive = true },
            new Host { Id = 8, FullName = "Meera Joshi", Email = "meera.joshi@example.com", Department = "Marketing", IsActive = true },
            new Host { Id = 9, FullName = "Arjun Desai", Email = "arjun.desai@example.com", Department = "IT Support", IsActive = true },
            new Host { Id = 10, FullName = "Divya Reddy", Email = "divya.reddy@example.com", Department = "Administration", IsActive = true },
            new Host { Id = 11, FullName = "Sanjay Gupta", Email = "sanjay.gupta@example.com", Department = "Procurement", IsActive = true },
            new Host { Id = 12, FullName = "Neha Bhatt", Email = "neha.bhatt@example.com", Department = "Engineering", IsActive = true });
    }
}

public class PurposeConfiguration : IEntityTypeConfiguration<Purpose>
{
    public void Configure(EntityTypeBuilder<Purpose> b)
    {
        b.ToTable("Purposes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Name).IsUnique();

        b.HasData(
            new Purpose { Id = 1, Name = "Business Meeting", IsActive = true },
            new Purpose { Id = 2, Name = "Interview", IsActive = true },
            new Purpose { Id = 3, Name = "Delivery", IsActive = true },
            new Purpose { Id = 4, Name = "Vendor Service / Maintenance", IsActive = true },
            new Purpose { Id = 5, Name = "Training / Workshop", IsActive = true },
            new Purpose { Id = 6, Name = "Audit / Inspection", IsActive = true },
            new Purpose { Id = 7, Name = "Other", IsActive = true });
    }
}

public class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
{
    public void Configure(EntityTypeBuilder<Visitor> b)
    {
        b.ToTable("Visitors");
        b.HasKey(x => x.Id);

        b.Property(x => x.VisitorName).IsRequired().HasMaxLength(100);
        b.Property(x => x.CompanyName).IsRequired().HasMaxLength(150);
        b.Property(x => x.MobileNumber).IsRequired().HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Remarks).HasMaxLength(500);
        b.Property(x => x.VisitDate).HasColumnType("datetime2");
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.RowVersion).IsRowVersion();   // optimistic concurrency

        b.HasOne(x => x.Host).WithMany()
         .HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Purpose).WithMany()
         .HasForeignKey(x => x.PurposeId).OnDelete(DeleteBehavior.Restrict);

        // Indexes for list search, sort, and filter
        b.HasIndex(x => x.VisitDate);
        b.HasIndex(x => x.VisitorName);
        b.HasIndex(x => x.HostId);
        b.HasIndex(x => x.Status);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.MessageId).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntityName).IsRequired().HasMaxLength(50);
        b.Property(x => x.EntityId).IsRequired().HasMaxLength(50);
        b.Property(x => x.Action).IsRequired().HasMaxLength(50);
        b.Property(x => x.Source).IsRequired().HasMaxLength(20);
        b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

        b.HasIndex(x => x.MessageId).IsUnique();   // idempotent Function processing
        b.HasIndex(x => x.CorrelationId);
    }
}