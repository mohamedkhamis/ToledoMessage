using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class EncryptedMessageConfiguration : IEntityTypeConfiguration<EncryptedMessage>
{
    public void Configure(EntityTypeBuilder<EncryptedMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(m => m.ConversationId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(m => m.SenderDeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(m => m.RecipientDeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(m => new { m.RecipientDeviceId, m.IsDelivered });
        builder.Property(m => m.Ciphertext).IsRequired().HasMaxLength(67_584);
        builder.Property(m => m.MessageType).IsRequired();
        builder.Property(m => m.ContentType).IsRequired();
        builder.Property(m => m.SequenceNumber).IsRequired();
        builder.Property(m => m.ServerTimestamp).IsRequired();
        builder.Property(m => m.IsDelivered).IsRequired().HasDefaultValue(false);

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.SenderDevice)
            .WithMany()
            .HasForeignKey(m => m.SenderDeviceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(m => m.RecipientDevice)
            .WithMany()
            .HasForeignKey(m => m.RecipientDeviceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
