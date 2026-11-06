using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Api.Middleware;

namespace Prisma.Workspace.Tests;

public class ArgumentErrorMappingTests
{
    [Theory]
    [InlineData("A tarefa pai não compartilha nenhum quadro com o item sendo criado.")]
    [InlineData("A etapa especificada não pertence ao projeto da tarefa.")]
    public async Task InvalidArgumentReturnsBadRequestProblem(string message)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddLogging().AddOptions().BuildServiceProvider();
        var middleware = new MapeamentoDeErrosMiddleware(_ => throw new ArgumentException(message),
            NullLogger<MapeamentoDeErrosMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(message, problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnexpectedErrorStillReturnsGenericInternalError()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddLogging().AddOptions().BuildServiceProvider();
        var middleware = new MapeamentoDeErrosMiddleware(_ => throw new InvalidOperationException("internal detail"),
            NullLogger<MapeamentoDeErrosMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.DoesNotContain("internal detail", await reader.ReadToEndAsync());
    }
}
