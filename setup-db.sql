USE [master];
GO

-- Create login for IIS app pool
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'IIS APPPOOL\ToledoMessage')
    CREATE LOGIN [IIS APPPOOL\ToledoMessage] FROM WINDOWS;
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'ToledoMessage')
    CREATE DATABASE [ToledoMessage];
GO

USE [ToledoMessage];
GO

-- Create user for the login
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'IIS APPPOOL\ToledoMessage')
    CREATE USER [IIS APPPOOL\ToledoMessage] FOR LOGIN [IIS APPPOOL\ToledoMessage];
GO

-- Grant db_owner role
ALTER ROLE [db_owner] ADD MEMBER [IIS APPPOOL\ToledoMessage];
GO
