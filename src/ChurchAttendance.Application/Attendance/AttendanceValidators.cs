using FluentValidation;

namespace ChurchAttendance.Application.Attendance;

public class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(x => x.MemberIds)
            .NotNull()
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Select at least one member to check in.");
    }
}

public class ScanQrCodeRequestValidator : AbstractValidator<ScanQrCodeRequest>
{
    public ScanQrCodeRequestValidator()
    {
        RuleFor(x => x.QrCode)
            .NotEmpty()
            .MaximumLength(200);
    }
}
