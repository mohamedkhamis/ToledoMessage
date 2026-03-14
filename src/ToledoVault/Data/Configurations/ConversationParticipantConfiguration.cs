using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasKey(static cp => new { cp.ConversationId, cp.UserId });
        builder.Property(static cp => cp.JoinedAt).IsRequired();
        builder.Property(static cp => cp.Role).IsRequired();
        builder.HasOne(static cp => cp.Conversation)
            .WithMany(static c => c.Participants)
            .HasForeignKey(static cp => cp.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(static cp => cp.User)
            .WithMany(static u => u.ConversationParticipants)
            .HasForeignKey(static cp => cp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
