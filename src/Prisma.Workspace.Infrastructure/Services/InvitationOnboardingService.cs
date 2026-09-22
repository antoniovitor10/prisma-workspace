using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;

namespace Prisma.Workspace.Infrastructure.Services;

public sealed class InvitationOnboardingService(AppDbContext db, UserManager<IdentityUser> users)
    : IInvitationOnboardingService
{
    private static string Hash(string token)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(token) && token.Length <= 512,
            "Convite inválido ou indisponível.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    private static void ValidateInvitation(OrganizationInvitation? invite)
        => DomainException.Garantir(invite is not null && invite.Organization.IsActive
            && invite.CanBeAccepted(DateTimeOffset.UtcNow), "Convite expirado ou indisponível.");

    public async Task<InvitationPreview> PreviewAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var invite = await db.OrganizationInvitations.IgnoreQueryFilters().AsNoTracking()
            .Include(i => i.Organization).SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        ValidateInvitation(invite);
        var existing = await users.FindByEmailAsync(invite!.Email);
        // Cadastro público sem confirmação não é conta utilizável: o convite
        // deve oferecer o mesmo fluxo de criação, não pedir uma senha que a
        // pessoa nunca chegou a usar.
        var accountExists = existing is not null && (existing.EmailConfirmed
            || await db.OrganizationMembers.IgnoreQueryFilters().AnyAsync(m => m.UserId == existing.Id, ct));
        return new(invite.Email, invite.Organization.Name, accountExists);
    }

    public async Task<InvitationCompletion> CompleteAsync(CompleteInvitationCommand request, CancellationToken ct)
    {
        var hash = Hash(request.Token);
        DomainException.Garantir(!string.IsNullOrEmpty(request.Password) && request.Password.Length <= 128,
            "Informe uma senha válida.");
        if (request.CreateAccount)
        {
            DomainException.Garantir(request.Password == request.ConfirmPassword, "As senhas não coincidem.");
            DomainException.Garantir(request.FullName?.Trim().Length <= 200
                && request.FullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 2,
                "Informe seu nome e sobrenome.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Serializa o consumo do mesmo token em todas as instâncias da API.
        var invite = await db.OrganizationInvitations
            .FromSqlInterpolated($"SELECT * FROM [OrganizationInvitations] WITH (UPDLOCK, HOLDLOCK) WHERE [TokenHash] = {hash}")
            .IgnoreQueryFilters().Include(i => i.Organization).SingleOrDefaultAsync(ct);
        ValidateInvitation(invite);
        var user = await users.FindByEmailAsync(invite!.Email);
        if (request.CreateAccount)
        {
            if (user is not null)
            {
                var hasMembership = await db.OrganizationMembers.IgnoreQueryFilters()
                    .AnyAsync(m => m.UserId == user.Id, ct);
                DomainException.Garantir(!user.EmailConfirmed && !hasMembership,
                    "Este e-mail já está cadastrado. Entre na sua conta.");
                var deleted = await users.DeleteAsync(user);
                DomainException.Garantir(deleted.Succeeded,
                    string.Join(" ", deleted.Errors.Select(e => e.Description)));
                user = null;
            }
            user = new IdentityUser { UserName = invite!.Email.Trim(), Email = invite.Email.Trim(), EmailConfirmed = true };
            var created = await users.CreateAsync(user, request.Password);
            DomainException.Garantir(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));
        }
        else
        {
            DomainException.Garantir(user is not null && !await users.IsLockedOutAsync(user)
                && await users.CheckPasswordAsync(user, request.Password), "E-mail ou senha inválidos.");
        }

        DomainException.Garantir(!await db.OrganizationMembers.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == user!.Id && m.OrganizationId != invite.OrganizationId, ct),
            "Esta conta já pertence a outra organização.");
        var member = await db.OrganizationMembers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == user!.Id, ct);
        if (member is null)
        {
            member = OrganizationMember.Create(invite.OrganizationId, user!.Id, invite.Role);
            if (request.CreateAccount) member.UpdateDisplayName(request.FullName!);
            db.OrganizationMembers.Add(member);
        }
        else DomainException.Garantir(member.IsActive, "Seu acesso à organização está desativado. Fale com o administrador.");

        if (!user!.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var updated = await users.UpdateAsync(user);
            DomainException.Garantir(updated.Succeeded, "Não foi possível confirmar a conta.");
        }
        invite.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(user.Id, invite.OrganizationId);
    }
}
