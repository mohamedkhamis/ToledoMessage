using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(c => c.Type).IsRequired();
        builder.Property(c => c.GroupName).HasMaxLength(200);
        builder.Property(c => c.CreatedAt).IsRequired();
    }
}
