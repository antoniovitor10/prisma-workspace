using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Services;

/// <summary>Porta os padrões do redigir.py do zapmind para o contrato da SPEC-AI-001.</summary>
public sealed class AiRedactionService : IAiRedactionService
{
    private const string Mask = "[oculto]";
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly RegexOptions Common = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // URLs e cabeçalhos são tratados antes dos rótulos, para não deixar fragmentos de credenciais.
    private static readonly Regex[] Patterns =
    [
        Create(@"https?://[^\s/]+:[^\s/]+@\S+", RegexOptions.IgnoreCase),
        Create(@"https?://\S*(?:token|senha|password|auth|verificar-email)\S*", RegexOptions.IgnoreCase),
        Create(@"\bAuthorization\s*[:=]\s*(?:Bearer|Basic)\s+\S+", RegexOptions.IgnoreCase),
        Create(@"\bBearer\s+\S+", RegexOptions.IgnoreCase),
        Create(@"\b(?:chave\s+pix|pix\s*[:=])[^\r\n]*", RegexOptions.IgnoreCase),
        Create(@"\b000201\S{40,}"),
        Create(@"\b(?:senha|password|pass|pwd|palavra[- ]passe|token|api[- _]?key|secret|chave[- ]api|authorization)\b\s*[:=]?\s*(?:""[^""\r\n]*""|'[^'\r\n]*'|\S+)", RegexOptions.IgnoreCase),
        Create(@"\b(?:usuario|usuário|user|login)\b\s*[:=]\s*\S+", RegexOptions.IgnoreCase),
        Create(@"^[ \t]*[\w.+-]+@[\w.-]+\.\w+[,;]?[ \t]*\r?\n[ \t]*(?=\S*[A-Z])(?=\S*\d)\S{6,}[ \t]*$", RegexOptions.Multiline),
        Create(@"^[ \t]*\S*(?=\S*[A-Z])(?=\S*\d)(?=\S*[!@#$%^&*])\S{7,}[ \t]*\r?$", RegexOptions.Multiline)
    ];

    private static readonly Regex Card = Create(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)");
    private static readonly Regex BusinessLabel = Create(@"\b(?:CPF|CNPJ)\s*[:=]?\s*$", RegexOptions.IgnoreCase);

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        try
        {
            var output = text;
            foreach (var pattern in Patterns)
                output = pattern.Replace(output, Mask);

            return Card.Replace(output, match =>
            {
                var digits = new string(match.Value.Where(char.IsDigit).ToArray());
                var prefix = output.Substring(Math.Max(0, match.Index - 16), Math.Min(16, match.Index));
                // CPF/CNPJ são dados de negócio. CNPJ sem pontuação também é preservado.
                if (digits.Length == 14 && (BusinessLabel.IsMatch(prefix) || IsCnpj(digits)))
                    return match.Value;
                return Mask;
            });
        }
        catch (RegexMatchTimeoutException)
        {
            // Na dúvida o texto inteiro é ocultado; o erro nunca carrega a entrada sensível.
            return Mask;
        }
    }

    private static Regex Create(string pattern, RegexOptions options = RegexOptions.None)
        => new(pattern, Common | options, MatchTimeout);

    private static bool IsCnpj(string digits)
    {
        if (digits.Distinct().Count() == 1) return false;
        int[] firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return CheckDigit(digits, firstWeights) == digits[12] - '0'
            && CheckDigit(digits, secondWeights) == digits[13] - '0';
    }

    private static int CheckDigit(string digits, int[] weights)
    {
        var remainder = weights.Select((weight, index) => weight * (digits[index] - '0')).Sum() % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
