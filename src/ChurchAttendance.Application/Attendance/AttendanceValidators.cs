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

public class CreateVipCheckInRequestValidator : AbstractValidator<CreateVipCheckInRequest>
{
    public CreateVipCheckInRequestValidator()
    {
        When(x => x.ExistingMemberId is null, () =>
        {
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(x => x.PhoneNo)
                .Matches(@"^\+?[0-9\s\-]{7,20}$")
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo))
                .WithMessage("Phone number must contain 7-20 digits and may start with +, spaces, or hyphens.");
        });
    }
}
