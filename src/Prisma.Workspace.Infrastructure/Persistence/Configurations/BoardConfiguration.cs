using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="Board"/>.
/// Mapeia a tabela "Boards" e define restrições, índices e relacionamentos.
/// </summary>
public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Boards");

        builder.HasKey(b => b.Id);

        // --- Propriedades escalares ---

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.OwnerId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.CardSettingsJson)
            .HasColumnType("nvarchar(max)");

        // --- Índices ---

        builder.HasIndex(b => b.OwnerId);
        builder.HasIndex(b => b.OrganizationId);
        builder.HasIndex(b => b.ProjectId);
        builder.HasIndex(b => b.TeamId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(b => b.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Relacionamentos ---

        builder.HasMany(b => b.Stages)
            .WithOne(s => s.Board)
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.WorkItems)
            .WithOne(w => w.Board)
            .HasForeignKey(w => w.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(b => b.Description)
            .HasMaxLength(1000);

        builder.HasOne(b => b.Client)
            .WithMany(c => c.Boards)
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(b => b.ClientId);

        builder.HasOne(b => b.Project)
            .WithMany(p => p.Boards)
            .HasForeignKey(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Team)
            .WithMany()
            .HasForeignKey(b => b.TeamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
