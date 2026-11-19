using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurchAttendance.Infrastructure.Persistence;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("smallint").ValueGeneratedNever();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();
        builder.HasData(
            new Role { Id = 1, Name = RoleNames.Member },
            new Role { Id = 2, Name = RoleNames.CellLeader },
            new Role { Id = 3, Name = RoleNames.Admin });
    }
}

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(m => m.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(m => m.PhoneNo).HasColumnName("phone_no").HasMaxLength(30);
        builder.Property(m => m.QrToken)
            .HasColumnName("qr_token")
            .HasMaxLength(64)
            .IsRequired()
            .HasDefaultValueSql("replace(gen_random_uuid()::text || gen_random_uuid()::text, '-', '')");
        builder.Property(m => m.CellId).HasColumnName("cell_id");
        builder.Property(m => m.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Ignore(m => m.FullName);

        builder.HasIndex(m => m.Email).IsUnique();
        builder.HasIndex(m => m.QrToken).IsUnique();
        builder.HasIndex(m => m.CellId).HasDatabaseName("ix_members_cell_id");
        builder.HasIndex(m => new { m.LastName, m.FirstName }).HasDatabaseName("ix_members_last_name");

        builder.HasOne(m => m.Cell)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.CellId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.User)
            .WithOne(u => u.Member)
            .HasForeignKey<User>(u => u.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CellConfiguration : IEntityTypeConfiguration<Cell>
{
    public void Configure(EntityTypeBuilder<Cell> builder)
    {
        builder.ToTable("cells");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.LeaderMemberId).HasColumnName("leader_member_id");
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.LeaderMemberId)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_cells_leader");

        builder.HasOne(c => c.Leader)
            .WithOne(m => m.LedCell)
            .HasForeignKey<Cell>(c => c.LeaderMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MemberRoleConfiguration : IEntityTypeConfiguration<MemberRole>
{
    public void Configure(EntityTypeBuilder<MemberRole> builder)
    {
        builder.ToTable("member_roles");
        builder.HasKey(mr => new { mr.MemberId, mr.RoleId });
        builder.Property(mr => mr.MemberId).HasColumnName("member_id");
        builder.Property(mr => mr.RoleId).HasColumnName("role_id").HasColumnType("smallint");

        builder.HasOne(mr => mr.Member)
            .WithMany(m => m.MemberRoles)
            .HasForeignKey(mr => mr.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mr => mr.Role)
            .WithMany(r => r.Members)
            .HasForeignKey(mr => mr.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.MemberId).HasColumnName("member_id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.FailedAttempts).HasColumnName("failed_attempts").HasDefaultValue(0);
        builder.Property(u => u.LockedUntil).HasColumnName("locked_until");
        builder.Property(u => u.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(false);
        builder.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.MemberId).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.RememberMe).HasColumnName("remember_me").HasDefaultValue(false);
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Ignore(t => t.IsActive);

        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_refresh_tokens_user");
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EventConfiguration : IEntityTypeConfiguration<Domain.Entities.Event>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Event> builder)
    {
        builder.ToTable("events", table =>
        {
            table.HasCheckConstraint("ck_events_dates", "end_date >= start_date");
            table.HasCheckConstraint("ck_events_single", "is_multi_day OR end_date = start_date");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsMultiDay).HasColumnName("is_multi_day").HasDefaultValue(false);
        builder.Property(e => e.StartDate).HasColumnName("start_date");
        builder.Property(e => e.EndDate).HasColumnName("end_date");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(e => e.StartDate).HasDatabaseName("ix_events_start_date");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EventDayConfiguration : IEntityTypeConfiguration<EventDay>
{
    public void Configure(EntityTypeBuilder<EventDay> builder)
    {
        builder.ToTable("event_days");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.EventId).HasColumnName("event_id");
        builder.Property(d => d.EventDate).HasColumnName("event_date");

        builder.HasIndex(d => new { d.EventId, d.EventDate }).IsUnique();
        builder.HasOne(d => d.Event)
            .WithMany(e => e.Days)
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Domain.Entities.Attendance>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Attendance> builder)
    {
        builder.ToTable("attendance");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.EventDayId).HasColumnName("event_day_id");
        builder.Property(a => a.MemberId).HasColumnName("member_id");
        builder.Property(a => a.CheckedInAt).HasColumnName("checked_in_at").HasDefaultValueSql("now()");
        builder.Property(a => a.RecordedBy).HasColumnName("recorded_by");

        builder.HasIndex(a => new { a.EventDayId, a.MemberId }).IsUnique();
        builder.HasIndex(a => a.MemberId).HasDatabaseName("ix_attendance_member");

        builder.HasOne(a => a.EventDay)
            .WithMany(d => d.AttendanceRecords)
            .HasForeignKey(a => a.EventDayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Member)
            .WithMany(m => m.AttendanceRecords)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.RecordedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
