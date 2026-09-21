using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests.Infrastructure;

public class HtmlSanitizerServiceTests
{
    [Fact]
    public void Sanitize_PreservesRichDescriptionAndRemovesExecutableContent()
    {
        var sanitizer = new HtmlSanitizerService();
        const string html = """
            <h2>Entrega</h2>
            <p><strong>Texto</strong> <a href="https://example.com">seguro</a></p>
            <ul data-type="taskList"><li data-type="taskItem" data-checked="true">Concluído</li></ul>
            <script>alert('xss')</script>
            <img src="https://example.com/image.png" onerror="alert('xss')">
            """;

        var result = sanitizer.Sanitize(html);

        Assert.Contains("<h2>Entrega</h2>", result);
        Assert.Contains("data-type=\"taskList\"", result);
        Assert.Contains("data-checked=\"true\"", result);
        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
    }
}
