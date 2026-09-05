namespace Detran.Kanban.Application.Interfaces;

/// <summary>Sanitiza HTML vindo do editor rico antes de persistir (proteção XSS).</summary>
public interface IHtmlSanitizer
{
    string Sanitize(string? html);
}
