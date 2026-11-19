using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChurchAttendance.Application.Attendance;
using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Dashboard;
using ChurchAttendance.Application.Events;
using ChurchAttendance.Application.Members;
using ChurchAttendance.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ChurchAttendance.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private static readonly string DatabaseName = $"church-integration-{Guid.NewGuid():N}";

    static ApiFactory()
    {
        // Minimal hosting reads configuration while Program.cs is executing, before
        // WebApplicationFactory's ConfigureAppConfiguration callback is applied. Environment
        // variables are available immediately, so use them to isolate the integration test.
        Environment.SetEnvironmentVariable("Database__Provider", "InMemory");
        Environment.SetEnvironmentVariable("Database__Name", DatabaseName);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ChurchAttendance.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ChurchAttendance.Tests");
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "INTEGRATION_TEST_SIGNING_KEY_LONG_ENOUGH_FOR_HMAC_SHA256_1234567890");
        Environment.SetEnvironmentVariable("Seed__AdminUsername", "admin");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", "ChangeMe#123!");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", "admin@church.local");
        Environment.SetEnvironmentVariable("Https__Redirect", "false");

        // WebApplicationFactory otherwise falls back to a solution-relative guess.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ChurchAttendance.Api");
            if (File.Exists(Path.Combine(candidate, "ChurchAttendance.Api.csproj")))
            {
                const string setting = "TEST_CONTENTROOT_CHURCHATTENDANCE_API";
                Environment.SetEnvironmentVariable(setting, candidate);
                Environment.SetEnvironmentVariable($"ASPNETCORE_{setting}", candidate);
                return;
            }
            directory = directory.Parent;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
                ["Database:Name"] = DatabaseName,
                ["Jwt:Issuer"] = "ChurchAttendance.Tests",
                ["Jwt:Audience"] = "ChurchAttendance.Tests",
                ["Jwt:Key"] = "INTEGRATION_TEST_SIGNING_KEY_LONG_ENOUGH_FOR_HMAC_SHA256_1234567890",
                ["Seed:AdminUsername"] = "admin",
                ["Seed:AdminPassword"] = "ChangeMe#123!",
                ["Seed:AdminEmail"] = "admin@church.local",
                ["Https:Redirect"] = "false"
            });
        });
    }
}

public class EndToEndFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EndToEndFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ChangePassword_CreateMember_QrCheckIn_Dashboard()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "ChangeMe#123!",
            rememberMe = true
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.True(auth!.User.MustChangePassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var blocked = await client.GetAsync("/api/members");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var change = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "ChangeMe#123!",
            newPassword = "NewAdmin#123"
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var relogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "NewAdmin#123",
            rememberMe = true
        });
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
        var auth2 = await relogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth2);
        Assert.False(auth2!.User.MustChangePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2.AccessToken);

        var memberResponse = await client.PostAsJsonAsync("/api/members", new
        {
            lastName = "Dela Cruz",
            firstName = "Juan",
            email = "juan.integration@example.com",
            phoneNo = "+639171234567",
            roles = new[] { "Member" }
        });
        Assert.Equal(HttpStatusCode.Created, memberResponse.StatusCode);
        var member = await memberResponse.Content.ReadFromJsonAsync<MemberDto>();
        Assert.NotNull(member);

        var qrResponse = await client.GetAsync($"/api/members/{member!.Id}/qr-code");
        Assert.Equal(HttpStatusCode.OK, qrResponse.StatusCode);
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<MemberQrCodeDto>();
        Assert.NotNull(qrCode);
        Assert.StartsWith(MemberQrCode.PayloadPrefix, qrCode!.Payload);

        var philippines = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, philippines).DateTime);
        var eventResponse = await client.PostAsJsonAsync("/api/events", new
        {
            name = "Integration Sunday",
            description = "Created by the integration test",
            isMultiDay = false,
            startDate = today,
            endDate = today
        });
        Assert.Equal(HttpStatusCode.Created, eventResponse.StatusCode);
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(createdEvent);
        Assert.Single(createdEvent!.Days);

        var scanPath = $"/api/events/{createdEvent.Id}/days/{createdEvent.Days[0].Id}/attendance/scan";

        var invalidScan = await client.PostAsJsonAsync(scanPath, new { qrCode = "not-a-member-qr" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidScan.StatusCode);

        var scan = await client.PostAsJsonAsync(scanPath, new { qrCode = qrCode.Payload });
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var scanResult = await scan.Content.ReadFromJsonAsync<ScanCheckInResultDto>();
        Assert.NotNull(scanResult);
        Assert.False(scanResult!.AlreadyCheckedIn);
        Assert.Equal(member.FullName, scanResult.MemberName);
        Assert.Equal(1, scanResult.CheckedInCount);

        var rescan = await client.PostAsJsonAsync(scanPath, new { qrCode = qrCode.Payload });
        Assert.Equal(HttpStatusCode.OK, rescan.StatusCode);
        var rescanResult = await rescan.Content.ReadFromJsonAsync<ScanCheckInResultDto>();
        Assert.NotNull(rescanResult);
        Assert.True(rescanResult!.AlreadyCheckedIn);

        var rotate = await client.PostAsync($"/api/members/{member.Id}/qr-code/rotate", null);
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var rotated = await rotate.Content.ReadFromJsonAsync<MemberQrCodeDto>();
        Assert.NotNull(rotated);
        Assert.NotEqual(qrCode.Payload, rotated!.Payload);

        var oldQrScan = await client.PostAsJsonAsync(scanPath, new { qrCode = qrCode.Payload });
        Assert.Equal(HttpStatusCode.NotFound, oldQrScan.StatusCode);

        var newQrScan = await client.PostAsJsonAsync(scanPath, new { qrCode = rotated.Payload });
        Assert.Equal(HttpStatusCode.OK, newQrScan.StatusCode);
        var newQrScanResult = await newQrScan.Content.ReadFromJsonAsync<ScanCheckInResultDto>();
        Assert.NotNull(newQrScanResult);
        Assert.True(newQrScanResult!.AlreadyCheckedIn);

        var kpis = await client.GetFromJsonAsync<DashboardKpisDto>("/api/dashboard/kpis");
        Assert.NotNull(kpis);
        Assert.Equal(2, kpis!.TotalActiveMembers);
        Assert.Equal(1, kpis.LastEvent?.Attendance);
        Assert.True(kpis.AverageAttendance > 0);

        var monthly = await client.GetAsync("/api/dashboard/monthly-comparison");
        Assert.Equal(HttpStatusCode.OK, monthly.StatusCode);
    }
}
