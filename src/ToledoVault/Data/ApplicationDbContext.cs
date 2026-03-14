using Microsoft.EntityFrameworkCore;
using ToledoVault.Models;

namespace ToledoVault.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<OneTimePreKey> OneTimePreKeys => Set<OneTimePreKey>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<EncryptedMessage> EncryptedMessages => Set<EncryptedMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<EncryptedKeyBackup> EncryptedKeyBackups => Set<EncryptedKeyBackup>();
    public DbSet<ConversationReadPointer> ConversationReadPointers => Set<ConversationReadPointer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
