/*
  IT Update: v1.5.0 Platform Administration Update
  Creates the runtime system settings store used by Administration > System Settings.
  The application also performs a safe IF-NOT-EXISTS bootstrap for this table at runtime.
*/
IF OBJECT_ID('dbo.SystemSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings
    (
        SettingKey nvarchar(200) NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
        SettingValue nvarchar(max) NULL,
        IsEncrypted bit NOT NULL CONSTRAINT DF_SystemSettings_IsEncrypted DEFAULT(0),
        UpdatedDate datetime2 NOT NULL CONSTRAINT DF_SystemSettings_UpdatedDate DEFAULT(SYSDATETIME())
    );
END;
GO
