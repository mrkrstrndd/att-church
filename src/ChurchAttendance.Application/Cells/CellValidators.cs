using FluentValidation;

namespace ChurchAttendance.Application.Cells;

public class CreateCellRequestValidator : AbstractValidator<CreateCellRequest>
{
    public CreateCellRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.LeaderMemberId).NotEmpty();
    }
}

public class UpdateCellRequestValidator : AbstractValidator<UpdateCellRequest>
{
    public UpdateCellRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.LeaderMemberId).NotEmpty();
    }
}

public class AddCellMembersRequestValidator : AbstractValidator<AddCellMembersRequest>
{
    public AddCellMembersRequestValidator()
    {
        RuleFor(x => x.MemberIds)
            .NotNull()
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Select at least one member.");
    }
}
