namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public sealed class BoardConnectionConfiguration : IEntityTypeConfiguration<BoardConnection>
{
    public void Configure(EntityTypeBuilder<BoardConnection> builder)
    {
        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.OrganizationUrl)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(connection => connection.ProjectName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(connection => connection.TeamOrAreaPath)
            .HasMaxLength(400);

        // Base64 plus the Data Protection envelope, so sized well above a raw access token.
        builder.Property(connection => connection.EncryptedCredential)
            .HasMaxLength(2_000);

        builder.Property(connection => connection.StateMappingJson)
            .HasMaxLength(4_000);

        builder.Property(connection => connection.LastSyncStatus)
            .HasMaxLength(500);

        // Not unique: soft-deleted rows stay, so a squad that reconnects would collide with its
        // own tombstone, and a filtered unique constraint needs provider SQL the in-memory test
        // provider ignores. The one-connection rule lives in the service, where it is tested.
        builder.HasIndex(connection => connection.TeamId);

        builder.Ignore(connection => connection.HasStoredCredential);
    }
}
