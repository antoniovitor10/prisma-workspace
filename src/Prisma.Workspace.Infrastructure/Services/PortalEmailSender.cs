using System.Net;
using System.Net.Mail;
using Prisma.Workspace.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Prisma.Workspace.Infrastructure.Services;

public class PortalEmailSender : IPortalEmailSender, IApplicationEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PortalEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public PortalEmailSender(
        IConfiguration configuration,
        ILogger<PortalEmailSender> logger,
        IHostEnvironment environment)
        => (_configuration, _logger, _environment) = (configuration, logger, environment);

    public bool IsConfigured => HasSmtpConfiguration();
    public bool CanExposeLocalToken => _environment.IsDevelopment() && !IsConfigured;
    public bool CanExposeLocalCode => CanExposeLocalToken;

    public async Task<bool> SendVerificationCodeAsync(
        string email,
        string portalName,
        string code,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync(email, $"Código de acesso — {portalName}",
            $"Seu código de verificação é {code}. Ele expira em 10 minutos.", cancellationToken);
    }

    public Task<bool> SendRequestConfirmationAsync(
        string email,
        string portalName,
        string protocol,
        string accessKey,
        string trackingPath,
        string? confirmationMessage,
        CancellationToken cancellationToken = default)
        => SendAsync(email, $"Solicitação {protocol} recebida — {portalName}",
            $"{confirmationMessage ?? "Recebemos sua solicitação."}\n\nProtocolo: {protocol}\nChave de acompanhamento: {accessKey}\nAcompanhamento: {trackingPath}",
            cancellationToken);

    public Task<bool> SendInformationRequestedAsync(
        string email,
        string portalName,
        string protocol,
        string message,
        string trackingPath,
        CancellationToken cancellationToken = default)
        => SendAsync(email, $"Mais informações necessárias — {protocol}",
            $"A equipe de {portalName} solicitou mais informações:\n\n{message}\n\nResponda em: {trackingPath}",
            cancellationToken);

    public Task<bool> SendPublicReplyAsync(
        string email,
        string portalName,
        string protocol,
        string message,
        string trackingPath,
        CancellationToken cancellationToken = default)
        => SendAsync(email, $"Nova resposta — {protocol}",
            $"A equipe de {portalName} respondeu à sua solicitação:\n\n{message}\n\nAcompanhe e responda em: {trackingPath}",
            cancellationToken);

    private bool HasSmtpConfiguration()
    {
        var section = _configuration.GetSection("PortalEmail");
        return !string.IsNullOrWhiteSpace(section["Host"])
            && !string.IsNullOrWhiteSpace(section["Sender"]);
    }

    public async Task<bool> SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var section = _configuration.GetSection("PortalEmail");
        var host = section["Host"];
        var sender = section["Sender"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
        {
            _logger.LogWarning("E-mail não enviado: SMTP não configurado.");
            return false;
        }

        try
        {
            using var mail = new MailMessage(sender, email)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            using var client = new SmtpClient(host, section.GetValue("Port", 587))
            {
                EnableSsl = section.GetValue("EnableSsl", true)
            };
            var user = section["User"];
            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, section["Password"]);

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(mail, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Envio de e-mail é best-effort: falha de SMTP não deve derrubar
            // o cadastro nem os fluxos de portal. Loga e devolve false.
            _logger.LogError(ex, "Falha ao enviar e-mail para {Email}.", email);
            return false;
        }
    }
}
