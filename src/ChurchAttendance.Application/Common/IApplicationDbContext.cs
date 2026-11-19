using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Member> Members { get; }
    DbSet<MemberRole> MemberRoles { get; }
    DbSet<Role> Roles { get; }
    DbSet<Cell> Cells { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Event> Events { get; }
    DbSet<EventDay> EventDays { get; }
    DbSet<Domain.Entities.Attendance> Attendance { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
