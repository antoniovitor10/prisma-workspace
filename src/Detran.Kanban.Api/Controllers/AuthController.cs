using System.ComponentModel.DataAnnotations;
using Detran.Kanban.Api.OpenApi;
using Detran.Kanban.Api.Services;
using Detran.Kanban.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[Tags("Autenticação")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtTokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IApplicationEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        JwtTokenService tokens,
        IRefreshTokenService refreshTokens,
        IApplicationEmailSender emailSender,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AuthController> logger)
        => (_userManager, _signInManager, _tokens, _refreshTokens, _emailSender,
            _environment, _configuration, _logger)
            = (userManager, signInManager, tokens, refreshTokens, emailSender,
                environment, configuration, logger);

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = new IdentityUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return IdentityValidation(result);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildFrontendLink("confirm-email", user.Id, token);
        var emailSent = await _emailSender.SendAsync(user.Email!, "Confirme seu e-mail — Detran Kanban",
            $"Confirme seu cadastro acessando: {link}", ct);
        if (!emailSent)
            _logger.LogWarning("Cadastro do usuário {UserId} criado, mas o e-mail de confirmação não pôde ser enviado.", user.Id);
        else
            _logger.LogInformation("Cadastro criado para o usuário {UserId}; confirmação pendente.", user.Id);
        return Accepted(new RegisterResponse(true, user.Id,
            _emailSender.CanExposeLocalToken ? token : null));
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest(ApiErrors.Validation(new Dictionary<string, string[]>
                { ["token"] = ["Código de confirmação inválido."] }, HttpContext.TraceIdentifier));
        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded) return IdentityValidation(result, "token");
        _logger.LogInformation("E-mail confirmado para o usuário {UserId}.", user.Id);
        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            _logger.LogWarning("Falha de autenticação para usuário inexistente a partir de {IpAddress}.", ClientIp());
            return Unauthorized(ApiErrors.Problem(401, "invalid_credentials", "Falha de autenticação",
                "E-mail ou senha inválidos.", HttpContext.TraceIdentifier));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Usuário {UserId} bloqueado após tentativas inválidas.", user.Id);
            return StatusCode(StatusCodes.Status423Locked,
                ApiErrors.Problem(423, "account_locked", "Conta temporariamente bloqueada",
                    "Aguarde alguns minutos antes de tentar novamente.", HttpContext.TraceIdentifier));
        }
        if (result.IsNotAllowed)
            return Unauthorized(ApiErrors.Problem(401, "email_not_confirmed", "Confirmação pendente",
                "Confirme seu e-mail antes de entrar.", HttpContext.TraceIdentifier));
        if (!result.Succeeded)
        {
            _logger.LogWarning("Falha de autenticação para o usuário {UserId}.", user.Id);
            return Unauthorized(ApiErrors.Problem(401, "invalid_credentials", "Falha de autenticação",
                "E-mail ou senha inválidos.", HttpContext.TraceIdentifier));
        }

        var refresh = await _refreshTokens.IssueAsync(user.Id, ClientIp(), ct);
        SetRefreshCookie(refresh);
        _logger.LogInformation("Autenticação concluída para o usuário {UserId}.", user.Id);
        return Ok(CreateAccessResponse(user));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var current))
            return Unauthorized(ApiErrors.Problem(401, "invalid_refresh_token", "Sessão expirada",
                "Entre novamente para continuar.", HttpContext.TraceIdentifier));
        var rotated = await _refreshTokens.RotateAsync(current, ClientIp(), ct);
        if (rotated is null)
        {
            ClearRefreshCookie();
            _logger.LogWarning("Refresh token inválido ou reutilizado a partir de {IpAddress}.", ClientIp());
            return Unauthorized(ApiErrors.Problem(401, "invalid_refresh_token", "Sessão expirada",
                "Entre novamente para continuar.", HttpContext.TraceIdentifier));
        }
        var user = await _userManager.FindByIdAsync(rotated.UserId);
        if (user is null || !user.EmailConfirmed || user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await _refreshTokens.RevokeFamilyAsync(rotated.Token, ClientIp(), ct);
            ClearRefreshCookie();
            return Unauthorized(ApiErrors.Problem(401, "invalid_refresh_token", "Sessão expirada",
                "Entre novamente para continuar.", HttpContext.TraceIdentifier));
        }
        SetRefreshCookie(rotated);
        return Ok(CreateAccessResponse(user));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var token))
            await _refreshTokens.RevokeFamilyAsync(token, ClientIp(), ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(RecoveryAcceptedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.EmailConfirmed) return Accepted(new RecoveryAcceptedResponse());
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var link = BuildFrontendLink("reset-password", user.Id, token);
        await _emailSender.SendAsync(user.Email!, "Recuperação de senha — Detran Kanban",
            $"Redefina sua senha acessando: {link}", ct);
        _logger.LogInformation("Recuperação de senha solicitada para o usuário {UserId}.", user.Id);
        return Accepted(new RecoveryAcceptedResponse(
            _emailSender.CanExposeLocalToken ? user.Id : null,
            _emailSender.CanExposeLocalToken ? token : null));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest(ApiErrors.Validation(new Dictionary<string, string[]>
                { ["token"] = ["Código de recuperação inválido."] }, HttpContext.TraceIdentifier));
        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return IdentityValidation(result, "newPassword");
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ClientIp(), ct);
        _logger.LogInformation("Senha redefinida e sessões revogadas para o usuário {UserId}.", user.Id);
        return NoContent();
    }

    private AuthResponse CreateAccessResponse(IdentityUser user)
    {
        var (accessToken, expiresIn) = _tokens.GenerateFor(user);
        return new AuthResponse(accessToken, "Bearer", expiresIn);
    }

    private IActionResult IdentityValidation(IdentityResult result, string field = "password")
        => BadRequest(ApiErrors.Validation(new Dictionary<string, string[]>
        {
            [field] = result.Errors.Select(x => x.Description).Distinct().ToArray()
        }, HttpContext.TraceIdentifier));

    private string BuildFrontendLink(string mode, string userId, string token)
    {
        var baseUrl = (_configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/auth?mode={mode}&userId={Uri.EscapeDataString(userId)}" +
            $"&token={Uri.EscapeDataString(token)}";
    }

    private string RefreshCookieName => _environment.IsDevelopment()
        ? "detran_refresh"
        : "__Host-detran_refresh";

    private void SetRefreshCookie(RefreshTokenIssue refresh)
        => Response.Cookies.Append(RefreshCookieName, refresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = refresh.ExpiresAt,
            IsEssential = true
        });

    private void ClearRefreshCookie()
        => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(128, MinimumLength = 10)] string Password);
public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(128)] string Password);
public sealed record ConfirmEmailRequest(
    [Required] string UserId,
    [Required] string Token);
public sealed record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(320)] string Email);
public sealed record ResetPasswordRequest(
    [Required] string UserId,
    [Required] string Token,
    [Required, StringLength(128, MinimumLength = 10)] string NewPassword);
public sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn);
public sealed record RegisterResponse(bool RequiresEmailConfirmation, string UserId, string? DevelopmentToken);
public sealed record RecoveryAcceptedResponse(string? DevelopmentUserId = null, string? DevelopmentToken = null);
