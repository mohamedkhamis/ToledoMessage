USE [ToledoMessage];
GO

-- EF Core migrations history table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

-- Migration: 20260225002144_InitialCreate
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260225002144_InitialCreate')
BEGIN
    CREATE TABLE [Conversations] (
        [Id] decimal(28,8) NOT NULL,
        [Type] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [DisappearingTimerSeconds] int NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id])
    );

    CREATE TABLE [Users] (
        [Id] decimal(28,8) NOT NULL,
        [DisplayName] nvarchar(50) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );

    CREATE TABLE [ConversationParticipants] (
        [ConversationId] decimal(28,8) NOT NULL,
        [UserId] decimal(28,8) NOT NULL,
        [JoinedAt] datetimeoffset NOT NULL,
        [Role] int NOT NULL,
        CONSTRAINT [PK_ConversationParticipants] PRIMARY KEY ([ConversationId], [UserId]),
        CONSTRAINT [FK_ConversationParticipants_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ConversationParticipants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );

    CREATE TABLE [Devices] (
        [Id] decimal(28,8) NOT NULL,
        [UserId] decimal(28,8) NOT NULL,
        [DeviceName] nvarchar(100) NOT NULL,
        [IdentityPublicKeyClassical] varbinary(max) NOT NULL,
        [IdentityPublicKeyPostQuantum] varbinary(max) NOT NULL,
        [SignedPreKeyPublic] varbinary(max) NOT NULL,
        [SignedPreKeySignature] varbinary(max) NOT NULL,
        [SignedPreKeyId] int NOT NULL,
        [KyberPreKeyPublic] varbinary(max) NOT NULL,
        [KyberPreKeySignature] varbinary(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [LastSeenAt] datetimeoffset NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Devices_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );

    CREATE TABLE [EncryptedMessages] (
        [Id] decimal(28,8) NOT NULL,
        [ConversationId] decimal(28,8) NOT NULL,
        [SenderDeviceId] decimal(28,8) NOT NULL,
        [RecipientDeviceId] decimal(28,8) NOT NULL,
        [Ciphertext] varbinary(max) NOT NULL,
        [MessageType] int NOT NULL,
        [ContentType] int NOT NULL,
        [SequenceNumber] bigint NOT NULL,
        [ServerTimestamp] datetimeoffset NOT NULL,
        [IsDelivered] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeliveredAt] datetimeoffset NULL,
        CONSTRAINT [PK_EncryptedMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EncryptedMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_EncryptedMessages_Devices_RecipientDeviceId] FOREIGN KEY ([RecipientDeviceId]) REFERENCES [Devices] ([Id]),
        CONSTRAINT [FK_EncryptedMessages_Devices_SenderDeviceId] FOREIGN KEY ([SenderDeviceId]) REFERENCES [Devices] ([Id])
    );

    CREATE TABLE [OneTimePreKeys] (
        [Id] decimal(28,8) NOT NULL,
        [DeviceId] decimal(28,8) NOT NULL,
        [KeyId] int NOT NULL,
        [PublicKey] varbinary(max) NOT NULL,
        [IsUsed] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_OneTimePreKeys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OneTimePreKeys_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_ConversationParticipants_UserId] ON [ConversationParticipants] ([UserId]);
    CREATE INDEX [IX_Devices_UserId] ON [Devices] ([UserId]);
    CREATE INDEX [IX_EncryptedMessages_ConversationId] ON [EncryptedMessages] ([ConversationId]);
    CREATE INDEX [IX_EncryptedMessages_RecipientDeviceId_IsDelivered] ON [EncryptedMessages] ([RecipientDeviceId], [IsDelivered]);
    CREATE INDEX [IX_EncryptedMessages_SenderDeviceId] ON [EncryptedMessages] ([SenderDeviceId]);
    CREATE UNIQUE INDEX [IX_OneTimePreKeys_DeviceId_KeyId] ON [OneTimePreKeys] ([DeviceId], [KeyId]);
    CREATE UNIQUE INDEX [IX_Users_DisplayName] ON [Users] ([DisplayName]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260225002144_InitialCreate', N'10.0.3');
END;
GO

-- Migration: 20260225011416_AddGroupName
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260225011416_AddGroupName')
BEGIN
    ALTER TABLE [Conversations] ADD [GroupName] nvarchar(200) NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260225011416_AddGroupName', N'10.0.3');
END;
GO
