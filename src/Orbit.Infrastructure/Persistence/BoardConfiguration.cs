using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orbit.Domain.Boards;
using Orbit.Domain.Projects;

namespace Orbit.Infrastructure.Persistence;

internal sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("boards");
        builder.HasKey(board => new { board.TenantId, board.ProjectId });
        builder.Property(board => board.TenantId).HasColumnName("tenant_id");
        builder.Property(board => board.ProjectId).HasColumnName("project_id");
        builder.Property(board => board.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(board => board.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
        builder.Property(board => board.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(board => board.CreatedAt).HasColumnName("created_at");
        builder.Property(board => board.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Project>().WithOne().HasForeignKey<Board>(board => new { board.TenantId, board.ProjectId })
            .HasPrincipalKey<Project>(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(board => board.Columns).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(board => board.Columns, column =>
        {
            column.ToTable("board_columns");
            column.WithOwner().HasForeignKey("tenant_id", "project_id");
            column.Property<Guid>("tenant_id").HasColumnName("tenant_id");
            column.Property<Guid>("project_id").HasColumnName("project_id");
            column.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
            column.Property(c => c.Order).HasColumnName("order");
            column.Property(c => c.WipLimit).HasColumnName("wip_limit");
            column.Property(c => c.WipLimitMode).HasColumnName("wip_limit_mode").HasConversion<string>().HasMaxLength(16);
            column.HasKey("tenant_id", "project_id", nameof(BoardColumn.Status));
        });
    }
}
