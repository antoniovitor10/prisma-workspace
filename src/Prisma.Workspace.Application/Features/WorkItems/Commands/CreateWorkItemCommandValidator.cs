using FluentValidation;

namespace Prisma.Workspace.Application.Features.WorkItems.Commands;

/// <summary>
/// Validador do comando de criação de WorkItem.
/// </summary>
public class CreateWorkItemCommandValidator : AbstractValidator<CreateWorkItemCommand>
{
    public CreateWorkItemCommandValidator()
    {
        // Quadro: BoardId != empty OU BoardIds não vazia OU ProjectId informado
        RuleFor(w => w)
            .Must(w => w.BoardId != Guid.Empty
                || (w.BoardIds != null && w.BoardIds.Count > 0)
                || w.ProjectId.HasValue)
            .WithMessage("Informe BoardId, BoardIds ou ProjectId para definir o quadro da tarefa.");

        RuleFor(w => w.Title)
            .NotEmpty().WithMessage("O título da tarefa é obrigatório.")
            .MaximumLength(500).WithMessage("O título deve ter no máximo 500 caracteres.");

        RuleFor(w => w.Subtitle)
            .MaximumLength(300).WithMessage("O subtítulo deve ter no máximo 300 caracteres.");

        RuleFor(w => w.Position)
            .GreaterThanOrEqualTo(0)
            .LessThan(999_999_999_999d)
            .WithMessage("A posicao da tarefa esta fora do intervalo permitido.");

        RuleFor(w => w.EstimatedHours)
            .GreaterThan(0).When(w => w.EstimatedHours.HasValue)
            .WithMessage("A estimativa de horas deve ser maior que zero.");

        RuleFor(w => w.RemainingHours)
            .GreaterThanOrEqualTo(0).When(w => w.RemainingHours.HasValue);

        RuleFor(w => w.RequesterName).MaximumLength(200);
        RuleFor(w => w.RequesterEmail).MaximumLength(320).EmailAddress()
            .When(w => !string.IsNullOrWhiteSpace(w.RequesterEmail));
        RuleFor(w => w.AcceptanceCriteria).MaximumLength(8000);
        RuleFor(w => w)
            .Must(w => !w.StartDate.HasValue || !w.DueDate.HasValue || w.DueDate >= w.StartDate)
            .WithMessage("O prazo nao pode ser anterior a data de inicio.");
        RuleFor(w => w.RequesterName)
            .NotEmpty().When(w => w.Origin == Prisma.Workspace.Domain.Enums.WorkItemOrigin.ExternalPortal)
            .WithMessage("O nome do solicitante e obrigatorio para demandas externas.");
    }
}
