using Ganss.Xss;

namespace Detran.Kanban.Infrastructure.Services;

/// <summary>Implementação do sanitizador da aplicação baseada em Ganss.Xss.</summary>
public class HtmlSanitizerService : Application.Interfaces.IHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        // Imagens embutidas (base64) e por URL https são úteis no wiki.
        _sanitizer.AllowedSchemes.Add("data");
        // Classes do editor (formatação) e a referência de tarefa nos links internos.
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("data-work-item-id");
        _sanitizer.AllowedAttributes.Add("target");
        _sanitizer.AllowedTags.Add("figure");
        _sanitizer.AllowedTags.Add("figcaption");
    }

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
}
