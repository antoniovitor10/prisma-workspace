using Prisma.Workspace.Application;
using Prisma.Workspace.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Prisma.Workspace.Api.OpenApi;

// ─── Bootstrap do Serilog (antes do host, para capturar erros de inicialização) ───
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Prisma.Workspace.Api");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // ─── Application & Infrastructure ─────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Storage físico dos anexos (raiz definida aqui porque depende do ContentRoot).
    builder.Services.AddSingleton<Prisma.Workspace.Application.Interfaces.IFileStorage>(
        new Prisma.Workspace.Infrastructure.Storage.LocalFileStorage(
            Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments")));

    // ─── ASP.NET Core Identity ────────────────────────────────────────────────
    // Obs.: AddIdentityCore é registrado via IdentityDbContext na camada Infrastructure.
    // O Identity em si (UserManager, RoleManager etc.) é configurado aqui na Api.
    builder.Services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddEntityFrameworkStores<Prisma.Workspace.Infrastructure.Persistence.AppDbContext>();

    // ─── Autenticação JWT Bearer ───────────────────────────────────────────────
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"];
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        throw new InvalidOperationException("JWT Key ausente ou menor que 32 bytes.");
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                 = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("InternalUser", policy => policy.RequireAuthenticatedUser()
            .RequireClaim("email_verified", "true"));
        options.AddPolicy("ConfirmedEmail", policy => policy.RequireClaim("email_verified", "true"));
    });
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsJsonAsync(
                ApiErrors.Problem(429, "rate_limit_exceeded", "Limite de requisições excedido",
                    "Aguarde antes de tentar novamente.", context.HttpContext.TraceIdentifier),
                cancellationToken);
        };
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    // Por IP real (via ForwardedHeaders). Folga para times atrás de
                    // um IP compartilhado; o bloqueio de conta protege contra brute force.
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("external-submissions", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{httpContext.Connection.RemoteIpAddress}:{httpContext.Request.Path}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("external-public", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{httpContext.Connection.RemoteIpAddress}:{httpContext.Request.Path}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    // ─── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title   = "Detran Kanban API",
            Version = "v1",
            Description = "API interna do painel Kanban do Detran-SE."
        });
        c.OperationFilter<Prisma.Workspace.Api.OpenApi.OrganizationHeaderOperationFilter>();
        c.OperationFilter<Prisma.Workspace.Api.OpenApi.StandardResponsesOperationFilter>();

        // Habilita envio do Bearer token pelo Swagger UI.
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = ParameterLocation.Header,
            Description  = "Informe: Bearer {seu_token}"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id   = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => string.IsNullOrWhiteSpace(x.Key)
                            ? "request"
                            : char.ToLowerInvariant(x.Key[0]) + x.Key[1..],
                        x => x.Value!.Errors.Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "Valor inválido."
                                : error.ErrorMessage).Distinct().ToArray());
                return new BadRequestObjectResult(ApiErrors.Validation(errors, context.HttpContext.TraceIdentifier))
                    { ContentTypes = { "application/problem+json" } };
            };
        });
    builder.Services.AddSignalR();
    builder.Services.AddScoped<Prisma.Workspace.Application.Interfaces.IBoardRealtimeNotifier,
        Prisma.Workspace.Api.Realtime.SignalRBoardRealtimeNotifier>();
    builder.Services.AddScoped<Prisma.Workspace.Api.Services.JwtTokenService>();

    builder.Services.AddCors(options =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // O container só é acessível pelo Caddy (reverse proxy na rede interna),
        // então confiamos no X-Forwarded-For dele. Sem limpar estas listas, o
        // ASP.NET só confia em loopback e ignora o cabeçalho — fazendo TODOS os
        // clientes compartilharem o IP do Caddy (quebra rate limiting por IP).
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ──────────────────────────────────────────────────────────────────────────
    var app = builder.Build();
    // ──────────────────────────────────────────────────────────────────────────

    app.UseForwardedHeaders();
    // Serilog registra método, rota, status, duração, usuário e correlação; corpos e segredos não são lidos.
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
            diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
        };
    });

    // Converte DomainException/ValidationException/NaoEncontradoException em 400/404.
    app.UseMiddleware<Prisma.Workspace.Api.Middleware.MapeamentoDeErrosMiddleware>();
    app.UseMiddleware<Prisma.Workspace.Api.Middleware.SecurityHeadersMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Detran Kanban API v1");
            c.RoutePrefix = "swagger";
        });
    }

    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<Prisma.Workspace.Api.Middleware.AuditContextMiddleware>();
    app.UseMiddleware<Prisma.Workspace.Api.Middleware.OrganizationContextMiddleware>();
    app.UseAuthorization();

    app.UseDefaultFiles();
    // Cache correto do SPA: index.html sempre revalida (evita tela branca pos-deploy,
    // quando o HTML velho aponta pra assets com hash que nao existem mais);
    // assets com hash no nome podem cachear pra sempre.
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            var headers = context.Context.Response.Headers;
            if (context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                headers.CacheControl = "no-cache";
            else if (context.Context.Request.Path.StartsWithSegments("/assets"))
                headers.CacheControl = "public, max-age=31536000, immutable";
        }
    });

    app.MapControllers();
    app.MapHub<Prisma.Workspace.Api.Realtime.BoardHub>("/hubs/boards");
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers.CacheControl = "no-cache"
    });
    // ─── Endpoint de saúde (GET /health) ──────────────────────────────────────
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
       .WithName("Health")
       .WithTags("Infra")
       .AllowAnonymous();

    // ─── Inicialização e População de Dados (Seed) ───────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var dbContext = services.GetRequiredService<Prisma.Workspace.Infrastructure.Persistence.AppDbContext>();
            var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
            if (app.Environment.IsDevelopment())
            {
                var demoPassword = app.Configuration["Seed:DemoPassword"]
                    ?? throw new InvalidOperationException("Seed:DemoPassword não configurada nos User Secrets.");
                await Prisma.Workspace.Infrastructure.Persistence.DbInitializer.SeedDataAsync(
                    dbContext, userManager, demoPassword);
            }
            else
            {
                await dbContext.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ocorreu um erro ao aplicar as migrations ou seed data.");
        }
    }

    app.Run();
}
catch (Microsoft.Extensions.Hosting.HostAbortedException)
{
    // Encerramento esperado quando as ferramentas do EF Core constroem o host de design-time.
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao inicializar.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
