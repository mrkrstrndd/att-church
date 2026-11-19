using ChurchAttendance.Application.Auth;
using ChurchAttendance.Domain.Entities;
using FluentValidation;

namespace ChurchAttendance.Application.Members;

public class CreateLoginRequestValidator : AbstractValidator<CreateLoginRequest>
{
    public CreateLoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may only contain letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .Must(PasswordRules.IsStrong)
            .WithMessage(PasswordRules.Message);
    }
}

public class CreateMemberRequestValidator : AbstractValidator<CreateMemberRequest>
{
    public CreateMemberRequestValidator()
    {
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.PhoneNo)
            .Matches(@"^\+?[0-9\s\-]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo))
            .WithMessage("Phone number must contain 7-20 digits and may start with +, spaces, or hyphens.");

        RuleFor(x => x.Roles)
            .Must(r => r is null || r.Count > 0)
            .WithMessage("Select at least one role.");
        RuleForEach(x => x.Roles)
            .Must(r => RoleNames.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Member, CellLeader, or Admin.");

        When(x => x.CreateLogin is not null, () =>
        {
            RuleFor(x => x.CreateLogin!).SetValidator(new CreateLoginRequestValidator());
        });
    }
}

public class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.PhoneNo)
            .Matches(@"^\+?[0-9\s\-]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo))
            .WithMessage("Phone number must contain 7-20 digits and may start with +, spaces, or hyphens.");

        RuleFor(x => x.Roles)
            .NotNull()
            .Must(r => r is { Count: > 0 })
            .WithMessage("Select at least one role.");
        RuleForEach(x => x.Roles)
            .Must(r => RoleNames.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Member, CellLeader, or Admin.");
    }
}
