using FluentValidation;

namespace ChurchAttendance.Application.Events;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotNull().WithMessage("A start date is required.");
        RuleFor(x => x.EndDate)
            .NotNull()
            .When(x => x.IsMultiDay)
            .WithMessage("An end date is required for a multi-day event.");
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.IsMultiDay && x.StartDate is not null && x.EndDate is not null)
            .WithMessage("The end date must be on or after the start date.");
    }
}

public class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotNull().WithMessage("A start date is required.");
        RuleFor(x => x.EndDate)
            .NotNull()
            .When(x => x.IsMultiDay)
            .WithMessage("An end date is required for a multi-day event.");
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.IsMultiDay && x.StartDate is not null && x.EndDate is not null)
            .WithMessage("The end date must be on or after the start date.");
    }
}
