using System.Reflection;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests.MetodoGit;

/// <summary>
/// Testes adversariais da Prova sobre a política da fatia A: caminhos indiretos para o token
/// (agente ou humano) chegar a Em desenvolvimento sem a sessão humana da UI.
/// </summary>
public class MethodTransitionPolicyAdversarialTests
{
    private static readonly Guid Org = Guid.NewGuid(), Project = Guid.NewGuid(), Board = Guid.NewGuid(), OtherBoard = Guid.NewGuid();
    private static readonly Guid AgentPrincipal = Guid.NewGuid(), OtherPrincipal = Guid.NewGuid(), Task1 = Guid.NewGuid();
    private static readonly Dictionary<string, Guid> Stage = MethodStatusCodes.All.ToDictionary(s => s, _ => Guid.NewGuid());
    private static readonly Guid OtherBoardStage = Guid.NewGuid(), UnmappedStage = Guid.NewGuid();

    private static MethodProjectSnapshot ProjectSnapshot(int autonomy = 0) => new(Org, Project, "metodo", Board, "metodo",
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, MethodGitPhase.Webhooks, autonomy, 1, Stage);

    private static MethodTaskSnapshot TaskIn(string status, Guid? board = null, bool authorized = false,
        Guid? blockingPrincipal = null, string? blockingOrigin = null, bool resumeApproved = false) =>
        new(Org, Project, board ?? Board, Task1, "T-0001", status == "outro" ? OtherBoardStage : Stage[status], status,
            Guid.NewGuid(), authorized ? Guid.NewGuid() : null, authorized ? DateTimeOffset.UtcNow.AddHours(-1) : null, null,
            blockingPrincipal is null ? null : Guid.NewGuid(), blockingPrincipal, blockingOrigin, resumeApproved ? Guid.NewGuid() : null,
            true, false, new byte[8]);

    private static MethodActorIdentity Token(MethodRole role, MethodAuthenticationKind kind = MethodAuthenticationKind.MethodToken) =>
        new(kind, role, AgentPrincipal, role == MethodRole.Human ? "user-1" : $"method:{AgentPrincipal:D}", "Ator",
            Org, Project, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1),
            new HashSet<PlatformPermission> { PlatformPermission.Create, PlatformPermission.ChangeStatus }, "mcp", "corr");

    private static MethodTransitionPolicy Policy(MethodActorIdentity actor, MethodTaskSnapshot? task, MethodProjectSnapshot? project = null)
    {
        var store = DispatchProxy.Create<IMethodGitStore, StoreProxy>();
        ((StoreProxy)(object)store).Project = project ?? ProjectSnapshot();
        ((StoreProxy)(object)store).Task = task;
        return new MethodTransitionPolicy(new Context(actor), store, new AllowAll());
    }

    private static Task<MethodTransitionDecision> Evaluate(MethodActorIdentity actor, MethodTaskSnapshot? task, MethodMutation m)
        => Policy(actor, task).EvaluateAsync(m);

    // ---- Transferência de quadro: chegar em Em desenvolvimento do quadro do método vindo de fora ----

    [Theory]
    [InlineData(MethodRole.Human)]
    [InlineData(MethodRole.Agent)]
    public async Task Transfer_FromOtherBoardIntoInDevelopment_IsDenied(MethodRole role)
    {
        // Card nasce em outro quadro do mesmo projeto e é transferido direto para Em desenvolvimento do quadro do método.
        var task = TaskIn("outro", board: OtherBoard);
        var m = new MethodMutation(Project, OtherBoard, Task1, OtherBoardStage, Stage[MethodStatusCodes.InDevelopment],
            "transferência", DestinationBoardId: Board);
        var d = await Evaluate(Token(role), task, m);
        Assert.False(d.Allowed, $"{role}: transferir para Em desenvolvimento do quadro do método autorizaria sem sessão UI ({d.Code}).");
    }

    [Theory]
    [InlineData(MethodRole.Human)]
    [InlineData(MethodRole.Agent)]
    public async Task Transfer_FromOtherBoardIntoReady_ThenStart_StillNeedsUi(MethodRole role)
    {
        // Mesmo que a transferência para Pronta seja aceita, iniciar depois continua exigindo a UI.
        var start = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.Ready], Stage[MethodStatusCodes.InDevelopment], "iniciar");
        Assert.False((await Evaluate(Token(role), TaskIn(MethodStatusCodes.Ready), start)).Allowed);
    }

    [Theory]
    [InlineData(MethodRole.Human)]
    [InlineData(MethodRole.Agent)]
    public async Task Creation_InOtherBoardWithMethodBoardDestination_IsDenied(MethodRole role)
    {
        var m = new MethodMutation(Project, OtherBoard, null, null, Stage[MethodStatusCodes.InDevelopment], "criar",
            IsCreation: true, DestinationBoardId: Board);
        Assert.False((await Evaluate(Token(role), null, m)).Allowed);
    }

    // ---- Criação ----

    [Theory]
    [InlineData(MethodRole.Human)]
    [InlineData(MethodRole.Agent)]
    public async Task Creation_DirectlyInDevelopment_IsDenied(MethodRole role)
    {
        var m = new MethodMutation(Project, Board, null, null, Stage[MethodStatusCodes.InDevelopment], "criar", IsCreation: true);
        var d = await Evaluate(Token(role), null, m);
        Assert.False(d.Allowed);
        Assert.Equal("interactive_authorization_required", role == MethodRole.Human ? d.Code : "interactive_authorization_required");
    }

    [Fact]
    public async Task Creation_InUnmappedColumnOfMethodBoard_IsDenied()
    {
        var m = new MethodMutation(Project, Board, null, null, UnmappedStage, "criar", IsCreation: true);
        Assert.False((await Evaluate(Token(MethodRole.Agent), null, m)).Allowed);
    }

    // ---- Token humano ----

    [Theory]
    [InlineData(MethodStatusCodes.Backlog)]
    [InlineData(MethodStatusCodes.Ready)]
    [InlineData(MethodStatusCodes.Blocked)]
    [InlineData(MethodStatusCodes.InTest)]
    [InlineData(MethodStatusCodes.Completed)]
    public async Task HumanToken_CannotReachInDevelopmentFrom(string from)
    {
        var m = new MethodMutation(Project, Board, Task1, Stage[from], Stage[MethodStatusCodes.InDevelopment], "token humano");
        Assert.False((await Evaluate(Token(MethodRole.Human), TaskIn(from, authorized: true), m)).Allowed, from);
    }

    [Fact]
    public async Task HumanToken_ReviewBackToDevelopment_NeedsCurrentAuthorization()
    {
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.InReview], Stage[MethodStatusCodes.InDevelopment], "voltar");
        Assert.False((await Evaluate(Token(MethodRole.Human), TaskIn(MethodStatusCodes.InReview, authorized: false), m)).Allowed);
        Assert.True((await Evaluate(Token(MethodRole.Human), TaskIn(MethodStatusCodes.InReview, authorized: true), m)).Allowed);
    }

    [Fact]
    public async Task UiSession_IsNotRestrictedByTheMatrix()
    {
        var ui = Token(MethodRole.Human, MethodAuthenticationKind.UiSession);
        foreach (var from in MethodStatusCodes.All)
        foreach (var to in MethodStatusCodes.All)
        {
            var m = new MethodMutation(Project, Board, Task1, Stage[from], Stage[to], "");
            Assert.True((await Evaluate(ui, TaskIn(from), m)).Allowed, $"UI {from} -> {to}");
        }
    }

    // ---- Agente: retomada e outros atalhos ----

    [Fact]
    public async Task Agent_ResumeOfAnotherPrincipalsBlock_IsDenied_EvenWithApproval()
    {
        var task = TaskIn(MethodStatusCodes.Blocked, authorized: true, blockingPrincipal: OtherPrincipal,
            blockingOrigin: MethodStatusCodes.InDevelopment, resumeApproved: true);
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.Blocked], Stage[MethodStatusCodes.InDevelopment], "retomar");
        Assert.False((await Evaluate(Token(MethodRole.Agent), task, m)).Allowed);
    }

    [Theory]
    [InlineData(MethodStatusCodes.Backlog)]
    [InlineData(MethodStatusCodes.Ready)]
    [InlineData(MethodStatusCodes.InReview)]
    public async Task Agent_ResumeOfOwnBlockNotOriginatedInDevelopment_IsDenied(string origin)
    {
        var task = TaskIn(MethodStatusCodes.Blocked, authorized: true, blockingPrincipal: AgentPrincipal, blockingOrigin: origin, resumeApproved: true);
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.Blocked], Stage[MethodStatusCodes.InDevelopment], "retomar");
        Assert.False((await Evaluate(Token(MethodRole.Agent), task, m)).Allowed, origin);
    }

    [Fact]
    public async Task Agent_ResumeOwnAuthorizedBlockWithApproval_IsAllowed()
    {
        var task = TaskIn(MethodStatusCodes.Blocked, authorized: true, blockingPrincipal: AgentPrincipal,
            blockingOrigin: MethodStatusCodes.InDevelopment, resumeApproved: true);
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.Blocked], Stage[MethodStatusCodes.InDevelopment], "retomar");
        Assert.True((await Evaluate(Token(MethodRole.Agent), task, m)).Allowed);
    }

    [Fact]
    public async Task Agent_FromUnmappedColumnToDevelopment_IsDenied()
    {
        var m = new MethodMutation(Project, Board, Task1, UnmappedStage, Stage[MethodStatusCodes.InDevelopment], "coluna fora do mapa");
        Assert.False((await Evaluate(Token(MethodRole.Agent), TaskIn(MethodStatusCodes.Backlog), m)).Allowed);
    }

    [Fact]
    public async Task Agent_ReviewRequiresAutonomyZeroAndAuthorization()
    {
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.InDevelopment], Stage[MethodStatusCodes.InReview], "nível 0");
        Assert.False((await Evaluate(Token(MethodRole.Agent), TaskIn(MethodStatusCodes.InDevelopment, authorized: false), m)).Allowed);
        var policy = Policy(Token(MethodRole.Agent), TaskIn(MethodStatusCodes.InDevelopment, authorized: true), ProjectSnapshot(autonomy: 1));
        Assert.False((await policy.EvaluateAsync(m)).Allowed, "fora do nível 0 o agente aguarda o Git");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Agent_BlockWithoutReason_IsDenied(string reason)
    {
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.InDevelopment], Stage[MethodStatusCodes.Blocked], reason);
        Assert.False((await Evaluate(Token(MethodRole.Agent), TaskIn(MethodStatusCodes.InDevelopment, authorized: true), m)).Allowed);
    }

    [Fact]
    public async Task Agent_StaleOrigin_IsConflict()
    {
        // O cliente diz que a task está em Pronta, mas no banco ela está em Backlog.
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.Ready], Stage[MethodStatusCodes.Blocked], "bloquear");
        var ex = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Policy(Token(MethodRole.Agent), TaskIn(MethodStatusCodes.Backlog)).EnsureAllowedAsync(m));
        Assert.Equal(409, ex.StatusCode);
    }

    // ---- Sistema ----

    [Fact]
    public async Task SystemToken_OverHttp_CannotMoveEvenWithFactShapedRequest()
    {
        var fact = new VerifiedGitFact(Guid.NewGuid(), Project, Task1, Guid.NewGuid(), MethodGitFactCodes.PullRequestOpened, "1", "rev",
            DateTimeOffset.UtcNow, new string('b', 40), null, new byte[32]);
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.InDevelopment], Stage[MethodStatusCodes.InReview], "fato", GitFact: fact);
        Assert.False((await Evaluate(Token(MethodRole.System), TaskIn(MethodStatusCodes.InDevelopment, authorized: true), m)).Allowed);
    }

    [Fact]
    public async Task UnknownAuthentication_IsDenied()
    {
        var m = new MethodMutation(Project, Board, Task1, Stage[MethodStatusCodes.InDevelopment], Stage[MethodStatusCodes.Blocked], "x");
        Assert.False((await Evaluate(Token(MethodRole.Human, MethodAuthenticationKind.Unknown), TaskIn(MethodStatusCodes.InDevelopment), m)).Allowed);
    }

    // ---- Falsos ----

    private sealed class Context(MethodActorIdentity actor) : IMethodAccessContext { public MethodActorIdentity? Current => actor; }

    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization,
            Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsureAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization,
            Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Implementa só as leituras que a política usa; qualquer outra chamada falha o teste.</summary>
    public class StoreProxy : DispatchProxy
    {
        public MethodProjectSnapshot? Project;
        public MethodTaskSnapshot? Task;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IMethodGitStore.GetProjectAsync) => System.Threading.Tasks.Task.FromResult(Project),
            nameof(IMethodGitStore.GetTaskAsync) => System.Threading.Tasks.Task.FromResult(Task),
            nameof(IMethodGitStore.GetRepositoryAsync) => System.Threading.Tasks.Task.FromResult<GitRepositoryDescriptor?>(null),
            _ => throw new InvalidOperationException($"A política não deveria chamar {method?.Name}."),
        };
    }
}
