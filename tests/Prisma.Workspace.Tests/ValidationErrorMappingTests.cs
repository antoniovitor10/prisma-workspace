using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Api.Middleware;

namespace Prisma.Workspace.Tests;

public class ValidationErrorMappingTests
{
    [Fact]
    public async Task ValidationRetainsFieldErrorsWithoutAttemptedValues()
    {
        const string secret = "never-serialize-this-attempted-value";
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var failures = new[]
        {
            new ValidationFailure("FullName", "Informe seu nome e sobrenome.", "Ana"),
            new ValidationFailure("Password", "Informe uma senha válida.", secret),
        };
        var middleware = new MapeamentoDeErrosMiddleware(_ => throw new ValidationException(failures),
            NullLogger<MapeamentoDeErrosMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal("Informe seu nome e sobrenome.", Assert.Single(errors.GetProperty("fullName").EnumerateArray()).GetString());
        Assert.Equal("Informe uma senha válida.", Assert.Single(errors.GetProperty("password").EnumerateArray()).GetString());
        Assert.DoesNotContain(secret, payload);
        Assert.Equal("validation_error", document.RootElement.GetProperty("type").GetString());
    }
}
