using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Prisma.Workspace.Application.Features.Ai;

/// <summary>
/// Artigos da central de ajuda enviados pela tela junto com a pergunta (HU-0003, CA-4).
/// </summary>
/// <remarks>
/// A central de ajuda vive no bundle do front, então é a tela que manda o artigo da página atual
/// (e os relacionados). O texto é documentação do produto, não dado do workspace: por isso o
/// bloco montado aqui não passa pela redação (que esconderia "token" e "Secret" seguidos de
/// outra palavra e tornaria o artigo ilegível). Em troca, o formato é estrito e qualquer valor
/// com cara de segredo recusa o pedido inteiro.
/// </remarks>
public sealed partial class AiHelpContext
{
    public const int MaxArticles = 3;
    public const int MaxArticleLength = 8000;
    public const int MaxTotalLength = 20000;

    [GeneratedRegex(@"^[a-z0-9-]{1,80}/[a-z0-9-]{1,80}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RefPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex DatePattern();

    /// <summary>Formatos de credenciais reais. Os artigos citam só nomes de configuração.</summary>
    [GeneratedRegex(@"pmt_[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY|\bsk-[A-Za-z0-9_-]{16,}|\bAKIA[0-9A-Z]{16}\b|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}|(?=[A-Za-z0-9+/=_-]*\d)(?=[A-Za-z0-9+/=_-]*[A-Z])(?=[A-Za-z0-9+/=_-]*[a-z])[A-Za-z0-9+/=_-]{40,}", RegexOptions.CultureInvariant, 200)]
    private static partial Regex SecretShape();

    public sealed record Article(string Ref, string Title, string UpdatedAt, bool Stale, string Content);

    public string? Page { get; }
    public IReadOnlyList<Article> Articles { get; }

    private AiHelpContext(string? page, IReadOnlyList<Article> articles) { Page = page; Articles = articles; }

    /// <summary>Lê o campo <c>help</c> do pedido; devolve <c>null</c> quando a tela não mandou ajuda.</summary>
    public static AiHelpContext? FromRequest(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("help", out var help) || help.ValueKind == JsonValueKind.Null) return null;
        if (help.ValueKind != JsonValueKind.Object) throw Invalid();
        string? page = null;
        if (help.TryGetProperty("page", out var pageValue) && pageValue.ValueKind != JsonValueKind.Null)
        {
            page = pageValue.ValueKind == JsonValueKind.String ? pageValue.GetString() : throw Invalid();
            if (page is null || page.Length > 300 || !page.StartsWith('/') || page.Any(char.IsControl)) throw Invalid();
        }
        var articles = new List<Article>();
        if (help.TryGetProperty("articles", out var list) && list.ValueKind != JsonValueKind.Null)
        {
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > MaxArticles) throw Invalid();
            var total = 0;
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw Invalid();
                var reference = Text(item, "ref", 161);
                var title = Text(item, "title", 200);
                var updatedAt = Text(item, "updatedAt", 10);
                var content = Text(item, "content", MaxArticleLength);
                var stale = item.TryGetProperty("stale", out var staleValue) && staleValue.ValueKind == JsonValueKind.True;
                if (!RefPattern().IsMatch(reference) || !DatePattern().IsMatch(updatedAt)
                    || !DateOnly.TryParseExact(updatedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw Invalid();
                total += content.Length;
                if (total > MaxTotalLength) throw Invalid();
                if (SecretShape().IsMatch(title) || SecretShape().IsMatch(content))
                    throw new AiException(400, "O artigo de ajuda enviado contém um valor com formato de credencial e foi recusado.");
                articles.Add(new(reference, title.Trim(), updatedAt, stale, content));
            }
        }
        return new AiHelpContext(page, articles);
    }

    private static string Text(JsonElement item, string key, int max)
    {
        if (!item.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String) throw Invalid();
        var text = value.GetString() ?? "";
        if (text.Length is 0 || text.Length > max) throw Invalid();
        // Quebras de linha e tabulação são texto do artigo; outros controles não.
        if (text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw Invalid();
        return text;
    }

    private static AiException Invalid() => new(400, "Contexto de ajuda inválido.");

    /// <summary>
    /// Bloco anexado ao prompt de sistema. Fica fora da redação, então só leva texto validado acima.
    /// </summary>
    public string ToPrompt()
    {
        var prompt = new StringBuilder();
        prompt.Append("\nAJUDA DO PRODUTO");
        if (Page is not null) prompt.Append(" (página atual: ").Append(Page).Append(')');
        prompt.Append(": quando a pergunta for sobre como usar ou configurar o Prisma (\"preciso de ajuda\", \"como faço\", \"onde fica\"), ")
            .Append("responda em até 6 linhas usando SOMENTE os artigos abaixo, que são a documentação oficial do produto. ")
            .Append("Não invente telas, botões, passos ou configurações que não estejam neles. ")
            .Append("Termine a resposta com a referência da seção usada no formato [A:assunto/artigo#ancora] (as âncoras aparecem depois de cada título); a tela transforma essa referência no link \"Abrir na ajuda\". ")
            .Append("Se um artigo estiver marcado como desatualizado, avise que ele pode estar desatualizado. ")
            .Append("Nunca peça nem escreva valores de credenciais; cite só o nome da configuração. ")
            .Append("Prefira as palavras \"credencial\" e \"tokens\" (no plural): a resposta oculta \"token\" e \"secret\" quando vêm antes de outra palavra.");
        if (Articles.Count == 0)
        {
            prompt.Append("\nNão há artigo da central de ajuda para esta página nem para a pergunta. Se a pergunta for de uso do produto, diga que a ajuda ainda não tem um artigo sobre isso e indique a central de ajuda em /ajuda, sem inventar passos.");
            return prompt.ToString();
        }
        prompt.Append("\nSe a resposta não estiver nos artigos, diga que a ajuda não cobre isso e indique a central de ajuda em /ajuda.");
        foreach (var article in Articles)
        {
            prompt.Append("\n\n=== ARTIGO [A:").Append(article.Ref).Append("] ").Append(article.Title)
                .Append(" | atualizado em ").Append(article.UpdatedAt)
                .Append(" | pode estar desatualizado: ").Append(article.Stale ? "sim" : "não").Append(" ===\n")
                .Append(article.Content);
        }
        prompt.Append("\n=== FIM DOS ARTIGOS ===");
        return prompt.ToString();
    }
}
