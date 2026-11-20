namespace ChurchAttendance.Application.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ChurchAttendance";
    public string Audience { get; set; } = "ChurchAttendance.Web";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenHours { get; set; } = 12;
    public int RememberMeDays { get; set; } = 30;
}

public class ChurchSettings
{
    public const string SectionName = "Church";

    public string Name { get; set; } = "Identity Church";
    public string TimeZoneId { get; set; } = "Asia/Manila";
    public int DefaultPageSize { get; set; } = 20;
    public int MaxPageSize { get; set; } = 100;
    public int LoginMaxFailedAttempts { get; set; } = 5;
    public int LoginLockoutMinutes { get; set; } = 15;
}

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "ChangeMe#123!";
    public string AdminEmail { get; set; } = "admin@church.local";
    public string AdminFirstName { get; set; } = "System";
    public string AdminLastName { get; set; } = "Admin";
}
