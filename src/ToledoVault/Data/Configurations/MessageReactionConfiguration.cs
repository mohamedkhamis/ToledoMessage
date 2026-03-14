using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.HasKey(static r => r.Id);
        builder.Property(static r => r.Id).ValueGeneratedNever();
        builder.Property(static r => r.Emoji).IsRequired().HasMaxLength(32);
        builder.Property(static r => r.CreatedAt).IsRequired();

        // One user can only have one reaction with the same emoji on a message
        builder.HasIndex(static r => new { r.MessageId, r.UserId, r.Emoji }).IsUnique();

        builder.HasOne(static r => r.Message)
            .WithMany()
            .HasForeignKey(static r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(static r => r.User)
            .WithMany()
            .HasForeignKey(static r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
