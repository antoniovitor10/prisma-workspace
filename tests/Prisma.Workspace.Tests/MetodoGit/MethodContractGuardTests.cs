using System.Reflection;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests.MetodoGit;

/// <summary>
/// Testes adversariais da Prova sobre os contratos F0 da fase 2a: nenhum DTO de entrada HTTP
/// deixa o cliente escolher identidade, canal ou fato Git, e nenhuma resposta carrega segredo.
/// A descoberta é por reflexão, então um DTO novo que quebre a regra também falha.
/// </summary>
public class MethodContractGuardTests
{
    private static readonly Assembly Contracts = typeof(MoveMethodTaskRequest).Assembly;

    private static IEnumerable<Type> ContractTypes() => Contracts.GetTypes()
        .Where(t => t.Namespace == typeof(MoveMethodTaskRequest).Namespace && t.IsPublic && !t.IsAbstract);

    /// <summary>DTOs que chegam do cliente HTTP: sufixo Request, por convenção do contrato.</summary>
    private static IEnumerable<Type> RequestTypes() => ContractTypes()
        .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal) && t != typeof(MethodMoveRequest));

    public static TheoryData<Type> HttpRequests()
    {
        var data = new TheoryData<Type>();
        foreach (var t in RequestTypes()) data.Add(t);
        return data;
    }

    /// <summary>Nomes que, vindos do cliente, dariam autoridade (identidade, canal, prova Git, correlação).</summary>
    private static readonly string[] ForbiddenInputNames =
    {
        "Channel", "AuthenticationKind", "ActorId", "UserId", "OrganizationId", "CorrelationId",
        "GitFact", "Fact", "DeliveryId", "SemanticKey", "TokenId", "IsInteractiveHuman", "Capabilities",
        "PermissionCeiling", "ExpectedRowVersion", "ExpectedDeliveryRowVersion",
    };

    private static readonly Type[] ForbiddenInputTypes =
        { typeof(VerifiedGitFact), typeof(MethodActorIdentity), typeof(NormalizedGitEvent), typeof(GitWebhookInput) };

    [Fact]
    public void HttpRequests_AreDiscovered()
    {
        var names = RequestTypes().Select(t => t.Name).ToHashSet();
        foreach (var expected in new[] { nameof(MoveMethodTaskRequest), nameof(CreateMethodTaskRequest), nameof(IssueMethodTokenRequest),
                     nameof(ResumeMethodTaskRequest), nameof(RegisterGitRepositoryRequest), nameof(LinkObservedPullRequestRequest) })
            Assert.Contains(expected, names);
        Assert.DoesNotContain(nameof(MethodMoveRequest), names);
    }

    [Theory]
    [MemberData(nameof(HttpRequests))]
    public void HttpRequest_DoesNotLetClientChooseIdentityChannelOrGitFact(Type request)
    {
        foreach (var p in request.GetProperties())
        {
            Assert.DoesNotContain(p.Name, ForbiddenInputNames);
            Assert.DoesNotContain(p.PropertyType, ForbiddenInputTypes);
            Assert.DoesNotContain(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType, new[] { typeof(MethodAuthenticationKind) });
        }
    }

    [Theory]
    [MemberData(nameof(HttpRequests))]
    public void HttpRequest_CarriesSecretsOnlyAsReferences(Type request)
    {
        foreach (var p in request.GetProperties())
        {
            var name = p.Name;
            var looksSecret = name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Token", StringComparison.OrdinalIgnoreCase);
            if (!looksSecret) continue;
            Assert.True(name.EndsWith("SecretRef", StringComparison.Ordinal) || name == nameof(RegisterGitRepositoryRequest.PreviousSecretValidUntilUtc)
                    || name == nameof(UpdateGitRepositoryRequest.PreviousSecretValidUntilUtc),
                $"{request.Name}.{name} parece segredo em claro; o contrato só admite referência (*SecretRef).");
        }
    }

    [Fact]
    public void MoveRequest_HasOnlyStatusAndReason()
    {
        var props = typeof(MoveMethodTaskRequest).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "NewStatus", "Reason" }, props);
    }

    [Fact]
    public void ResumeRequest_DoesNotNamePrincipalOrRole()
    {
        var props = typeof(ResumeMethodTaskRequest).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("PrincipalId", props);
        Assert.DoesNotContain("Role", props);
    }

    [Fact]
    public void IssueTokenRequest_HasNoHumanUserIdToImpersonate()
    {
        // Revisão git-spec ALTO 2: token humano só para a própria pessoa; não existe parâmetro de outro usuário.
        var props = typeof(IssueMethodTokenRequest).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(props, n => n.Contains("UserId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(props, n => n.Contains("HumanUser", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Respostas e snapshots: nenhum campo de token/segredo/hash, exceto o token emitido uma vez.</summary>
    public static TheoryData<Type> Responses()
    {
        var data = new TheoryData<Type>();
        foreach (var t in ContractTypes().Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal) || t.Name.EndsWith("Page", StringComparison.Ordinal)))
            data.Add(t);
        return data;
    }

    [Theory]
    [MemberData(nameof(Responses))]
    public void Response_DoesNotExposeSecretMaterial(Type dto)
    {
        foreach (var p in dto.GetProperties())
        {
            if (dto == typeof(IssuedMethodTokenDto) && p.Name == nameof(IssuedMethodTokenDto.Token)) continue;
            Assert.False(p.Name is "Token" or "TokenHash" or "Hash" or "Secret" or "WebhookSecret" or "PrivateKey" or "Password",
                $"{dto.Name}.{p.Name} expõe material secreto.");
            Assert.False(p.Name.EndsWith("SecretRef", StringComparison.Ordinal) && dto == typeof(GitRepositoryDto),
                $"{dto.Name}.{p.Name}: a resposta pública do repositório não precisa expor referência de segredo.");
        }
    }

    [Fact]
    public void MethodTokenMetadata_HasNoSecretButKeepsPrefixForDisplay()
    {
        var props = typeof(MethodTokenDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.Contains("TokenPrefix", props);
        Assert.DoesNotContain("Token", props);
        Assert.DoesNotContain("TokenHash", props);
    }

    [Theory]
    [InlineData("Agente")]
    [InlineData("AGENTE")]
    [InlineData(" agente")]
    [InlineData("agente ")]
    [InlineData("Agent")]
    [InlineData("2")]
    [InlineData("human")]
    [InlineData("admin")]
    [InlineData("")]
    public void RoleParse_AcceptsOnlyExactMethodCodes(string role)
        => Assert.Throws<ArgumentException>(() => MethodRoleCodes.Parse(role));

    [Fact]
    public void RoleCodes_RoundTrip()
    {
        foreach (var role in new[] { MethodRole.Human, MethodRole.Agent, MethodRole.System })
            Assert.Equal(role, MethodRoleCodes.Parse(role.ToCode()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((MethodRole)0).ToCode());
    }

    [Theory]
    [InlineData("Backlog")]
    [InlineData("em revisao")]
    [InlineData("em-revisão")]
    [InlineData(" pronta")]
    [InlineData("concluída")]
    [InlineData("done")]
    [InlineData(null)]
    public void StatusCodes_AcceptOnlyTheSevenLiteralCodes(string? status)
        => Assert.False(MethodStatusCodes.IsValid(status));

    [Fact]
    public void StatusCodes_AreTheSevenOfTheContractInBoardOrder()
        => Assert.Equal(new[] { "backlog", "pronta", "em-desenvolvimento", "em-revisao", "em-teste", "concluida", "bloqueada" }, MethodStatusCodes.All);

    [Theory]
    [InlineData('\u0000')]
    [InlineData('=')]
    [InlineData('+')]
    [InlineData('/')]
    [InlineData('.')]
    [InlineData('\uFF41')] // 'a' de largura total
    [InlineData('\u0663')] // dígito arábico 3
    [InlineData(' ')]
    public void TokenFormat_RejectsAnyCharOutsideBase64Url(char invalid)
    {
        var valid = MethodTokenFormat.Prefix + new string('a', 43);
        Assert.True(MethodTokenFormat.IsWellFormed(valid));
        // Mesmo comprimento do válido: a recusa vem do caractere, não do tamanho.
        var withInvalid = MethodTokenFormat.Prefix + invalid + new string('a', 42);
        Assert.Equal(valid.Length, withInvalid.Length);
        Assert.False(MethodTokenFormat.IsWellFormed(withInvalid));
    }

    [Fact]
    public void InteractiveHuman_RequiresUiSessionAndHumanRole()
    {
        MethodActorIdentity Actor(MethodAuthenticationKind k, MethodRole r) => new(k, r, null, "u", "Nome", null, null, null, null,
            new HashSet<PlatformPermission>(), "ui", "c");
        Assert.True(Actor(MethodAuthenticationKind.UiSession, MethodRole.Human).IsInteractiveHuman);
        Assert.False(Actor(MethodAuthenticationKind.UiSession, MethodRole.Agent).IsInteractiveHuman);
        Assert.False(Actor(MethodAuthenticationKind.UiSession, MethodRole.System).IsInteractiveHuman);
    }
}
