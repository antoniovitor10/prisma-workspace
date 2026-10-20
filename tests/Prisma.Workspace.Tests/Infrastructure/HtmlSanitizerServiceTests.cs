using Prisma.Workspace.Infrastructure.Services;
using AngleSharp.Html.Parser;

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
            <img src="data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" data-attachment-id="4d1f4f0f-7f9a-4f19-89f2-c7a1ca75a67e" alt="imagem colada">
            """;

        var result = sanitizer.Sanitize(html);

        Assert.Contains("<h2>Entrega</h2>", result);
        Assert.Contains("data-type=\"taskList\"", result);
        Assert.Contains("data-checked=\"true\"", result);
        Assert.Contains("data-attachment-id=\"4d1f4f0f-7f9a-4f19-89f2-c7a1ca75a67e\"", result);
        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xhtml+xml")]
    public void Parser_TreatsAnnotationXmlAsHtmlIntegrationPoint(string encoding)
    {
        // O ponto de integração deve produzir HTML, igual ao navegador, sem executar conteúdo.
        var html = $"<math><annotation-xml encoding='{encoding}'><title>Descrição</title></annotation-xml></math>";
        var document = new HtmlParser().ParseDocument(html);
        var title = Assert.Single(document.QuerySelectorAll("title"));

        Assert.Equal("http://www.w3.org/1999/xhtml", title.NamespaceUri);
        Assert.Equal("Descrição", title.TextContent);
    }

    [Fact]
    public void Sanitize_PreservesEditorReferencesAndRemovesUnsafeLinksAndStyles()
    {
        var sanitizer = new HtmlSanitizerService();
        const string html = """
            <p class="editor"><a href="/items/123" data-work-item-id="123">Tarefa</a></p>
            <figure><img src="https://example.test/image.png" data-attachment-id="attachment" alt="Imagem"><figcaption>Legenda</figcaption></figure>
            <p style="color: red; background-image: url(javascript:invalid)">Texto</p>
            <a href="javascript:invalid" onclick="invalid()">Link</a>
            """;
        var result = sanitizer.Sanitize(html);
        Assert.Contains("data-work-item-id=\"123\"", result);
        Assert.Contains("data-attachment-id=\"attachment\"", result);
        Assert.Contains("<figcaption>Legenda</figcaption>", result);
        Assert.Contains("class=\"editor\"", result);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
    }
}
