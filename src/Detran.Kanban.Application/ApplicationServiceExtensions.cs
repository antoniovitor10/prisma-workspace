using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Detran.Kanban.Application;

/// <summary>
/// Extensões de DI para registrar os serviços da camada de Application.
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registra todos os handlers MediatR do assembly Application.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Registra todos os validators FluentValidation do assembly Application.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<Features.Reports.ReportExecutor>();

        // Executa os validators antes de cada handler (sem isso eles não rodam).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Common.Behaviors.ValidationBehavior<,>));

        return services;
    }
}
