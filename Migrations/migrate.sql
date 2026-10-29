IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE TABLE [Children] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [DateOfBirth] datetime2 NULL,
        [ParentId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Children] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Children_Users_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailOtps] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [OtpHash] nvarchar(500) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsUsed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailOtps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailOtps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE TABLE [Devices] (
        [Id] int NOT NULL IDENTITY,
        [DeviceName] nvarchar(100) NOT NULL,
        [DeviceIdentifier] nvarchar(255) NULL,
        [Platform] nvarchar(50) NULL,
        [ChildId] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Devices_Children_ChildId] FOREIGN KEY ([ChildId]) REFERENCES [Children] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE TABLE [MonitoringRules] (
        [Id] int NOT NULL IDENTITY,
        [RuleName] nvarchar(100) NOT NULL,
        [RuleType] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsEnabled] bit NOT NULL,
        [ChildId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MonitoringRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MonitoringRules_Children_ChildId] FOREIGN KEY ([ChildId]) REFERENCES [Children] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Children_ParentId] ON [Children] ([ParentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_ChildId] ON [Devices] ([ChildId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailOtps_UserId_ExpiresAt] ON [EmailOtps] ([UserId], [ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MonitoringRules_ChildId] ON [MonitoringRules] ([ChildId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029034917_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029034917_InitialCreate', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029043553_AddGoogleAuthToUser'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PasswordHash');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Users] ALTER COLUMN [PasswordHash] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029043553_AddGoogleAuthToUser'
)
BEGIN
    ALTER TABLE [Users] ADD [GoogleSubjectId] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029043553_AddGoogleAuthToUser'
)
BEGIN
    CREATE INDEX [IX_Users_GoogleSubjectId] ON [Users] ([GoogleSubjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029043553_AddGoogleAuthToUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029043553_AddGoogleAuthToUser', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029082226_AddDeviceLocation'
)
BEGIN
    CREATE TABLE [DeviceLocations] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [ChildId] int NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [Accuracy] float NULL,
        [RecordedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeviceLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeviceLocations_Children_ChildId] FOREIGN KEY ([ChildId]) REFERENCES [Children] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DeviceLocations_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029082226_AddDeviceLocation'
)
BEGIN
    CREATE INDEX [IX_DeviceLocations_ChildId_RecordedAt] ON [DeviceLocations] ([ChildId], [RecordedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029082226_AddDeviceLocation'
)
BEGIN
    CREATE INDEX [IX_DeviceLocations_DeviceId] ON [DeviceLocations] ([DeviceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029082226_AddDeviceLocation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029082226_AddDeviceLocation', N'8.0.3');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    ALTER TABLE [Devices] ADD [BatteryPercentage] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    ALTER TABLE [Devices] ADD [BatteryUpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    ALTER TABLE [Devices] ADD [IsCharging] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    CREATE TABLE [LocationRefreshRequests] (
        [Id] int NOT NULL IDENTITY,
        [ChildId] int NOT NULL,
        [DeviceId] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [FailureReason] nvarchar(500) NULL,
        CONSTRAINT [PK_LocationRefreshRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LocationRefreshRequests_Children_ChildId] FOREIGN KEY ([ChildId]) REFERENCES [Children] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LocationRefreshRequests_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    CREATE INDEX [IX_LocationRefreshRequests_ChildId_RequestedAt] ON [LocationRefreshRequests] ([ChildId], [RequestedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    CREATE INDEX [IX_LocationRefreshRequests_DeviceId_Status_ExpiresAt] ON [LocationRefreshRequests] ([DeviceId], [Status], [ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029090256_AddLocationRefreshAndBattery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029090256_AddLocationRefreshAndBattery', N'8.0.3');
END;
GO

COMMIT;
GO

