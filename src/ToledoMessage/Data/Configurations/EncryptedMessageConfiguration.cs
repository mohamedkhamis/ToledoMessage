using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class EncryptedMessageConfiguration : IEntityTypeConfiguration<EncryptedMessage>
{
    public void Configure(EntityTypeBuilder<EncryptedMessage> builder)
    {
        builder.HasKey(static m => m.Id);
        builder.Property(static m => m.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static m => m.ConversationId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static m => m.SenderDeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static m => m.RecipientDeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(static m => new { m.RecipientDeviceId, m.IsDelivered });
        builder.HasIndex(static m => new { m.ConversationId, m.SequenceNumber }).IsUnique();
        builder.Property(static m => m.Ciphertext).IsRequired().HasColumnType("varbinary(max)");
        builder.Property(static m => m.MessageType).IsRequired();
        builder.Property(static m => m.ContentType).IsRequired();
        builder.Property(static m => m.FileName).HasMaxLength(256);
        builder.Property(static m => m.MimeType).HasMaxLength(128);
        builder.Property(static m => m.SequenceNumber).IsRequired();
        builder.Property(static m => m.ServerTimestamp).IsRequired();
        builder.Property(static m => m.IsDelivered).IsRequired().HasDefaultValue(false);
        builder.HasOne(static m => m.Conversation)
            .WithMany(static c => c.Messages)
            .HasForeignKey(static m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(static m => m.SenderDevice)
            .WithMany()
            .HasForeignKey(static m => m.SenderDeviceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(static m => m.RecipientDevice)
            .WithMany()
            .HasForeignKey(static m => m.RecipientDeviceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
