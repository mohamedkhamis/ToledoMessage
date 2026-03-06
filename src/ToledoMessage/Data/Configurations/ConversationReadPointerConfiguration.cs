using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class ConversationReadPointerConfiguration : IEntityTypeConfiguration<ConversationReadPointer>
{
    public void Configure(EntityTypeBuilder<ConversationReadPointer> builder)
    {
        builder.HasKey(static p => new { p.UserId, p.ConversationId });
        builder.Property(static p => p.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static p => p.ConversationId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static p => p.LastReadSequenceNumber).IsRequired().HasDefaultValue(0L);
        builder.Property(static p => p.UnreadCount).IsRequired().HasDefaultValue(0);

        builder.HasOne(static p => p.User)
            .WithMany()
            .HasForeignKey(static p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(static p => p.Conversation)
            .WithMany()
            .HasForeignKey(static p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
