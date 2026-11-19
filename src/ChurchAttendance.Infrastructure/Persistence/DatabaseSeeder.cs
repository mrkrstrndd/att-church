using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SeedOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IOptions<SeedOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var adminRole = await _db.Roles.SingleAsync(r => r.Name == RoleNames.Admin, ct);
        var username = _options.AdminUsername.Trim().ToLowerInvariant();
        var existingUser = await _db.Users
            .Include(u => u.Member).ThenInclude(m => m.MemberRoles)
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        if (existingUser is not null)
        {
            if (existingUser.Member.MemberRoles.All(mr => mr.RoleId != adminRole.Id))
            {
                existingUser.Member.MemberRoles.Add(new MemberRole
                {
                    MemberId = existingUser.MemberId,
                    RoleId = adminRole.Id
                });
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var email = _options.AdminEmail.Trim().ToLowerInvariant();
        var member = await _db.Members
            .Include(m => m.MemberRoles)
            .FirstOrDefaultAsync(m => m.Email == email, ct);

        if (member is null)
        {
            member = new Member
            {
                Id = Guid.NewGuid(),
                FirstName = _options.AdminFirstName.Trim(),
                LastName = _options.AdminLastName.Trim(),
                Email = email,
                IsActive = true
            };
            _db.Members.Add(member);
        }

        member.IsActive = true;
        if (member.MemberRoles.All(mr => mr.RoleId != adminRole.Id))
        {
            member.MemberRoles.Add(new MemberRole
            {
                MemberId = member.Id,
                RoleId = adminRole.Id
            });
        }

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            Username = username,
            PasswordHash = _passwordHasher.Hash(_options.AdminPassword),
            MustChangePassword = true,
            IsActive = true
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded default administrator {Username}; password change is required on first login.", username);
    }
}
