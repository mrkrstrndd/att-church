using ChurchAttendance.Application.Attendance;
using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Cells;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Dashboard;
using ChurchAttendance.Application.Events;
using ChurchAttendance.Application.Members;
using ChurchAttendance.Application.Vip;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IChurchClock, ChurchClock>();

        services.AddScoped<AuthService>();
        services.AddScoped<MemberService>();
        services.AddScoped<CellService>();
        services.AddScoped<EventService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<VipService>();

        return services;
    }
}
