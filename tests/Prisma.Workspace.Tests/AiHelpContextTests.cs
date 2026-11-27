using System.Text.Json;
using Prisma.Workspace.Application.Features.Ai;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0003, CA-4 e CA-9: artigos da ajuda enviados pela tela ao assistente.</summary>
public class AiHelpContextTests
{
    private static JsonElement Input(object help) => JsonSerializer.SerializeToElement(new { text = "como faço?", help });
    private static object Article(string reference = "integracoes/tokens", string content = "## Emitir (âncora: emitir)\nPassos.", bool stale = false, string updatedAt = "2026-11-27")
        => new { @ref = reference, title = "Tokens", updatedAt, stale, content };

    [Fact]
    public void WithoutHelpFieldReturnsNull()
        => Assert.Null(AiHelpContext.FromRequest(JsonSerializer.SerializeToElement(new { text = "Quais são minhas tarefas?" })));

    [Fact]
    public void PromptCitesTheArticleReferenceAndTheStaleFlag()
    {
        var help = AiHelpContext.FromRequest(Input(new { page = "/projects/1/settings", articles = new[] { Article(stale: true) } }))!;
        var prompt = help.ToPrompt();
        Assert.Contains("[A:integracoes/tokens]", prompt);
        Assert.Contains("pode estar desatualizado: sim", prompt);
        Assert.Contains("página atual: /projects/1/settings", prompt);
        Assert.Contains("Abrir na ajuda", prompt);
    }

    [Fact]
    public void WithoutArticlesThePromptSaysThereIsNoHelp()
    {
        var prompt = AiHelpContext.FromRequest(Input(new { page = "/home", articles = Array.Empty<object>() }))!.ToPrompt();
        Assert.Contains("Não há artigo da central de ajuda", prompt);
        Assert.DoesNotContain("=== ARTIGO", prompt);
    }

    [Theory]
    [InlineData("../segredo")]
    [InlineData("integracoes")]
    [InlineData("Integracoes/Tokens")]
    public void RejectsReferencesOutsideTheArticleFormat(string reference)
        => Assert.Equal(400, Assert.Throws<AiException>(() => AiHelpContext.FromRequest(Input(new { articles = new[] { Article(reference) } }))).Status);

    [Fact]
    public void RejectsMoreArticlesThanTheLimit()
        => Assert.Throws<AiException>(() => AiHelpContext.FromRequest(Input(new { articles = Enumerable.Range(0, AiHelpContext.MaxArticles + 1).Select(_ => Article()).ToArray() })));

    [Fact]
    public void RejectsArticleLongerThanTheLimit()
        => Assert.Throws<AiException>(() => AiHelpContext.FromRequest(Input(new { articles = new[] { Article(content: new string('a', AiHelpContext.MaxArticleLength + 1)) } })));

    [Fact]
    public void RejectsInvalidDate()
        => Assert.Throws<AiException>(() => AiHelpContext.FromRequest(Input(new { articles = new[] { Article(updatedAt: "2026-02-31") } })));

    [Theory]
    [InlineData("pmt_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG")]
    [InlineData("gh" + "p_abcdefghijklmnopqrstuvwxyz0123")] // concatenado para o varredor de segredos não acusar o exemplo
    [InlineData("-----BEGIN RSA " + "PRIVATE KEY-----")]
    [InlineData("chave sk-abcdefghijklmnopqrstu")]
    public void RejectsCredentialShapedValues(string value)
    {
        var error = Assert.Throws<AiException>(() => AiHelpContext.FromRequest(Input(new { articles = new[] { Article(content: $"Texto {value} fim") } })));
        Assert.Contains("credencial", error.Message);
    }

    [Fact]
    public void AcceptsConfigurationNamesWithoutValues()
    {
        var help = AiHelpContext.FromRequest(Input(new { articles = new[] { Article(content: "Configure MethodGit:Enabled e MethodGit__Secrets__<nome> no servidor.") } }))!;
        Assert.Single(help.Articles);
    }
}
