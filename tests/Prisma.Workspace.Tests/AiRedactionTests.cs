using Prisma.Workspace.Infrastructure.Services;

namespace Prisma.Workspace.Tests;

public class AiRedactionTests
{
    private readonly AiRedactionService _service = new();

    [Theory]
    [InlineData("senha: Abc@1234", "Abc@1234")]
    [InlineData("PASSWORD = Abc@1234", "Abc@1234")]
    [InlineData("pwd Abc@1234", "Abc@1234")]
    [InlineData("palavra-passe: Abc@1234", "Abc@1234")]
    [InlineData("senha: \"segredo com espacos\"", "com espacos")]
    [InlineData("usuario: exemplo", "exemplo")]
    [InlineData("usuário=exemplo", "exemplo")]
    [InlineData("login: exemplo", "exemplo")]
    [InlineData("token: tokenTeste123", "tokenTeste123")]
    [InlineData("api_key=chaveTeste123", "chaveTeste123")]
    [InlineData("secret chaveTeste123", "chaveTeste123")]
    [InlineData("Authorization: Bearer tokenTeste123", "tokenTeste123")]
    [InlineData("Authorization: Basic dGVzdGU6dGVzdGU=", "dGVzdGU6dGVzdGU=")]
    [InlineData("Bearer tokenTeste123", "tokenTeste123")]
    [InlineData("chave Pix: cliente@example.invalid", "cliente@example.invalid")]
    [InlineData("Pix=11999990000", "11999990000")]
    [InlineData("00020112345678901234567890123456789012345678901234567890", "1234567890")]
    [InlineData("cartao 4111 1111 1111 1111", "4111")]
    [InlineData("cartao 4111-1111-1111-1111", "4111")]
    [InlineData("cliente@example.invalid\nAbc12345", "Abc12345")]
    [InlineData("cliente@example.invalid\r\nAbc12345", "Abc12345")]
    [InlineData("  Abc@1234  ", "Abc@1234")]
    [InlineData("https://usuario:senha@teste.example.invalid/path", "usuario")]
    [InlineData("https://teste.example.invalid/reset?token=segredo", "segredo")]
    [InlineData("https://teste.example.invalid/verificar-email/codigo", "codigo")]
    public void RemovesSecretValues(string input, string secret)
    {
        var result = _service.Redact(input);
        Assert.Contains("[oculto]", result);
        Assert.DoesNotContain(secret, result);
    }

    [Theory]
    [InlineData("CPF: 123.456.789-09")]
    [InlineData("CPF: 12345678909")]
    [InlineData("CNPJ: 12.345.678/0001-95")]
    [InlineData("CNPJ: 12345678000195")]
    [InlineData("12345678000195")]
    [InlineData("Projeto Prisma, tarefa 1002 com prazo amanhã.")]
    [InlineData("cliente@example.invalid")]
    public void PreservesBusinessIdentifiersAndOrdinaryText(string input)
        => Assert.Equal(input, _service.Redact(input));

    [Fact]
    public void MasksEachSecretAndPreservesOtherLines()
    {
        const string input = "Projeto Prisma\nsenha: Abc@1234\ntoken: chaveTeste123\nPrazo amanhã";
        Assert.Equal("Projeto Prisma\n[oculto]\n[oculto]\nPrazo amanhã", _service.Redact(input));
    }

    [Fact]
    public void RedactionIsIdempotent()
    {
        var masked = _service.Redact("senha: Abc@1234; Bearer tokenTeste123");
        Assert.Equal(masked, _service.Redact(masked));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputProducesEmptyOutput(string? input)
        => Assert.Equal(string.Empty, _service.Redact(input));
}
