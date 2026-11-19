using ChurchAttendance.Application.Common;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberRole> MemberRoles => Set<MemberRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Cell> Cells => Set<Cell>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Domain.Entities.Event> Events => Set<Domain.Entities.Event>();
    public DbSet<EventDay> EventDays => Set<EventDay>();
    public DbSet<Domain.Entities.Attendance> Attendance => Set<Domain.Entities.Attendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case Member member:
                        member.CreatedAt = member.CreatedAt == default ? now : member.CreatedAt;
                        member.UpdatedAt = now;
                        break;
                    case Cell cell:
                        cell.CreatedAt = cell.CreatedAt == default ? now : cell.CreatedAt;
                        cell.UpdatedAt = now;
                        break;
                    case Domain.Entities.Event ev:
                        ev.CreatedAt = ev.CreatedAt == default ? now : ev.CreatedAt;
                        ev.UpdatedAt = now;
                        break;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case Member member:
                        member.UpdatedAt = now;
                        break;
                    case Cell cell:
                        cell.UpdatedAt = now;
                        break;
                    case Domain.Entities.Event ev:
                        ev.UpdatedAt = now;
                        break;
                }
            }
        }
    }
}
