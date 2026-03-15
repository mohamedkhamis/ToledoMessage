using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(static c => c.Id);
        builder.Property(static c => c.Id).ValueGeneratedNever();
        builder.Property(static c => c.Type).IsRequired();
        builder.Property(static c => c.GroupName).HasMaxLength(200);
        builder.Property(static c => c.CreatedAt).IsRequired();
    }
}
