using System.Security.Cryptography;
using System.Text;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prisma.Workspace.Api.Controllers;
using Prisma.Workspace.Application.Features.Installation;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class InstallationSetupTests
{
    private static readonly string Valid32ByteTokenBase64Url = Convert.ToBase64String(Encoding.UTF8.GetBytes("12345678901234567890123456789012"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static readonly string ShortTokenBase64Url = Convert.ToBase64String(Encoding.UTF8.GetBytes("short-token-16b"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Base64UrlDecoding_ValidatesFormatAndLength()
    {
        Assert.True(InstallationSetupService.TryDecodeBase64Url(Valid32ByteTokenBase64Url, out var validBytes));
        Assert.True(validBytes.Length >= 32);

        Assert.True(InstallationSetupService.TryDecodeBase64Url(ShortTokenBase64Url, out var shortBytes));
        Assert.True(shortBytes.Length < 32);

        Assert.False(InstallationSetupService.TryDecodeBase64Url("invalid_base64!@#", out _));
        Assert.False(InstallationSetupService.TryDecodeBase64Url(null, out _));
        Assert.False(InstallationSetupService.TryDecodeBase64Url("   ", out _));
    }

    [Fact]
    public void Base64UrlDecoding_ConstantTimeComparisonValidation()
    {
        var token1 = Valid32ByteTokenBase64Url;
        var token2 = Valid32ByteTokenBase64Url + "diff";

        var hash1 = SHA256.HashData(Encoding.UTF8.GetBytes(token1));
        var hash2 = SHA256.HashData(Encoding.UTF8.GetBytes(token2));
        var hash1Copy = SHA256.HashData(Encoding.UTF8.GetBytes(token1));

        Assert.True(CryptographicOperations.FixedTimeEquals(hash1, hash1Copy));
        Assert.False(CryptographicOperations.FixedTimeEquals(hash1, hash2));
    }

    [Fact]
    public async Task Controller_GetStatus_ReturnsAnonymousStatusWithMinimalPayload()
    {
        var mediatorMock = new TestMediator(new InstallationSetupStatus(false, true));
        var controller = new SetupController(mediatorMock);

        var result = await controller.Status(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var status = Assert.IsType<InstallationSetupStatus>(okResult.Value);
        Assert.False(status.Initialized);
        Assert.True(status.SetupAvailable);
    }

    [Fact]
    public async Task Controller_Complete_ReturnsExpectedStatusCodesForOutcomes()
    {
        var mediatorMock = new TestMediator(new InstallationSetupResult(InstallationSetupOutcome.Created));
        var controller = new SetupController(mediatorMock)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Prisma-Setup-Token"] = Valid32ByteTokenBase64Url;

        var request = new CompleteSetupRequest("Admin", "admin@prisma.local", "P@ssword1234", "Org", "org");
        var createdResult = await controller.Complete(request, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(createdResult);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        mediatorMock.Result = new InstallationSetupResult(InstallationSetupOutcome.Unavailable);
        var unavailableResult = await controller.Complete(request, CancellationToken.None);
        var unavailableObject = Assert.IsType<ObjectResult>(unavailableResult);
        Assert.Equal(StatusCodes.Status403Forbidden, unavailableObject.StatusCode);

        mediatorMock.Result = new InstallationSetupResult(InstallationSetupOutcome.AlreadyCompleted);
        var completedResult = await controller.Complete(request, CancellationToken.None);
        var completedObject = Assert.IsType<ObjectResult>(completedResult);
        Assert.Equal(StatusCodes.Status409Conflict, completedObject.StatusCode);

        mediatorMock.Result = new InstallationSetupResult(InstallationSetupOutcome.Conflict);
        var conflictResult = await controller.Complete(request, CancellationToken.None);
        var conflictObject = Assert.IsType<ObjectResult>(conflictResult);
        Assert.Equal(StatusCodes.Status409Conflict, conflictObject.StatusCode);

        mediatorMock.Result = new InstallationSetupResult(InstallationSetupOutcome.ValidationFailed, new Dictionary<string, string[]>
        {
            ["AdministratorPassword"] = ["Senha muito curta."]
        });
        var validationResult = await controller.Complete(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(validationResult);
    }

    [Fact]
    public async Task Service_GetStatus_FailsClosedWhenSingletonIsMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"setup-status-missing-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "true",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var service = new InstallationSetupService(context, CreateUserManager(context), config);
        var status = await service.GetStatusAsync();

        Assert.False(status.Initialized);
        Assert.False(status.SetupAvailable);
    }

    [Fact]
    public async Task Service_GetStatus_RefusesWhenDisabledOrInvalidToken()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"setup-status-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.InstallationStates.Add(InstallationState.CreatePending());
        await context.SaveChangesAsync();

        var configDisabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "false",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var serviceDisabled = new InstallationSetupService(context, CreateUserManager(context), configDisabled);
        var statusDisabled = await serviceDisabled.GetStatusAsync();
        Assert.False(statusDisabled.Initialized);
        Assert.False(statusDisabled.SetupAvailable);

        var configShortToken = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "true",
            ["Setup:Token"] = ShortTokenBase64Url
        }).Build();

        var serviceShortToken = new InstallationSetupService(context, CreateUserManager(context), configShortToken);
        var statusShortToken = await serviceShortToken.GetStatusAsync();
        Assert.False(statusShortToken.Initialized);
        Assert.False(statusShortToken.SetupAvailable);
    }

    [Fact]
    public async Task Service_Complete_Refuses403WhenDisabledMissingOrInvalidToken()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"setup-refuse403-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new TestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.InstallationStates.Add(InstallationState.CreatePending());
        await context.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "true",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var service = new InstallationSetupService(context, CreateUserManager(context), config);
        var request = new InstallationSetupRequest("Admin User", "admin@prisma.local", "P@ssword1234!", "Empresa", "empresa");

        var resultNoToken = await service.CompleteAsync(request, null);
        Assert.Equal(InstallationSetupOutcome.Unavailable, resultNoToken.Outcome);

        var resultInvalidToken = await service.CompleteAsync(request, "token-invalido-12345678901234567890");
        Assert.Equal(InstallationSetupOutcome.Unavailable, resultInvalidToken.Outcome);

        var configDisabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "false",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var serviceDisabled = new InstallationSetupService(context, CreateUserManager(context), configDisabled);
        var resultDisabled = await serviceDisabled.CompleteAsync(request, Valid32ByteTokenBase64Url);
        Assert.Equal(InstallationSetupOutcome.Unavailable, resultDisabled.Outcome);
    }

    [Fact]
    public async Task Service_Complete_Refuses409WhenInitializedOrExistingData()
    {
        var dbName = $"setup-refuse409-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "true",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var request = new InstallationSetupRequest("Admin User", "admin@prisma.local", "P@ssword1234!", "Empresa", "empresa");

        await using (var context = new TestAppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.InstallationStates.Add(InstallationState.CreatePending());
            await context.SaveChangesAsync();
            var state = await context.InstallationStates.SingleAsync(x => x.Id == InstallationState.SingletonId);
            state.MarkInitialized(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();

            var userManager = CreateUserManager(context);
            var service = new InstallationSetupService(context, userManager, config);

            var resultCompleted = await service.CompleteAsync(request, Valid32ByteTokenBase64Url);
            Assert.Equal(InstallationSetupOutcome.AlreadyCompleted, resultCompleted.Outcome);
        }

        var options2 = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"setup-refuse409-2-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var context = new TestAppDbContext(options2))
        {
            await context.Database.EnsureCreatedAsync();
            context.InstallationStates.Add(InstallationState.CreatePending());
            await context.SaveChangesAsync();
            var userManager = CreateUserManager(context);
            await userManager.CreateAsync(new IdentityUser { UserName = "existing@prisma.local", Email = "existing@prisma.local" }, "P@ssword1234!");

            var service = new InstallationSetupService(context, userManager, config);
            var resultExistingData = await service.CompleteAsync(request, Valid32ByteTokenBase64Url);
            Assert.Equal(InstallationSetupOutcome.Conflict, resultExistingData.Outcome);
        }
    }

    [Fact]
    public async Task Service_Complete_SuccessfulFlowCreatesUserOrgMemberAndMarksSingleton()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"setup-success-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var context = new TestAppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.InstallationStates.Add(InstallationState.CreatePending());
            await context.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setup:Enabled"] = "true",
            ["Setup:Token"] = Valid32ByteTokenBase64Url
        }).Build();

        var request = new InstallationSetupRequest("Admin Master", "admin.master@prisma.local", "P@ssword1234!", "Organização Principal", "org-principal");

        await using (var context = new TestAppDbContext(options))
        {
            var userManager = CreateUserManager(context);
            var service = new InstallationSetupService(context, userManager, config);

            var result = await service.CompleteAsync(request, Valid32ByteTokenBase64Url);
            Assert.Equal(InstallationSetupOutcome.Created, result.Outcome);
        }

        await using (var assertionContext = new TestAppDbContext(options))
        {
            var state = await assertionContext.InstallationStates.SingleAsync(x => x.Id == InstallationState.SingletonId);
            Assert.True(state.IsInitialized);
            Assert.NotNull(state.InitializedAt);

            var user = await assertionContext.Users.SingleAsync(u => u.Email == "admin.master@prisma.local");
            Assert.Equal("admin.master@prisma.local", user.UserName);
            Assert.True(user.EmailConfirmed);

            var org = await assertionContext.Organizations.IgnoreQueryFilters().Include(o => o.Members).SingleAsync(o => o.Slug == "org-principal");
            Assert.Equal("Organização Principal", org.Name);

            var member = Assert.Single(org.Members);
            Assert.Equal(user.Id, member.UserId);
            Assert.Equal(OrganizationRole.Administrator, member.Role);
            Assert.Equal("Admin Master", member.DisplayName);
        }
    }

    [Fact]
    public void Validator_ValidatesFieldsCorrectly()
    {
        var validator = new CompleteInstallationSetupCommandValidator();

        var validCommand = new CompleteInstallationSetupCommand("Nome Valido", "email@dominio.com", "Password123!", "Organizacao", "org-slug", Valid32ByteTokenBase64Url);
        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);

        var invalidCommand = new CompleteInstallationSetupCommand("A", "email-invalido", "123", "", "Org Slug Com Espaco!", null);
        var invalidResult = validator.Validate(invalidCommand);
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Errors, e => e.PropertyName == nameof(CompleteInstallationSetupCommand.AdministratorName));
        Assert.Contains(invalidResult.Errors, e => e.PropertyName == nameof(CompleteInstallationSetupCommand.AdministratorEmail));
        Assert.Contains(invalidResult.Errors, e => e.PropertyName == nameof(CompleteInstallationSetupCommand.AdministratorPassword));
        Assert.Contains(invalidResult.Errors, e => e.PropertyName == nameof(CompleteInstallationSetupCommand.OrganizationName));
        Assert.Contains(invalidResult.Errors, e => e.PropertyName == nameof(CompleteInstallationSetupCommand.OrganizationSlug));
    }

    private static UserManager<IdentityUser> CreateUserManager(AppDbContext context)
    {
        var store = new UserStore<IdentityUser>(context);
        return new UserManager<IdentityUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<IdentityUser>(),
            [new UserValidator<IdentityUser>()],
            [new PasswordValidator<IdentityUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
#pragma warning disable CS8625
            null,
#pragma warning restore CS8625
            new Logger<UserManager<IdentityUser>>(new LoggerFactory()));
    }

    private class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<InstallationState>().Property(x => x.Version).IsRequired(false);
        }
    }

    private class TestMediator : MediatR.IMediator
    {
        public InstallationSetupResult? Result { get; set; }
        private readonly InstallationSetupStatus? _status;

        public TestMediator(InstallationSetupStatus status) => _status = status;
        public TestMediator(InstallationSetupResult result) => Result = result;

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest
            => Task.CompletedTask;

        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) == typeof(InstallationSetupStatus))
                return Task.FromResult((TResponse)(object)_status!);
            if (typeof(TResponse) == typeof(InstallationSetupResult))
                return Task.FromResult((TResponse)(object)Result!);
            throw new NotImplementedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(_status ?? (object?)Result);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
