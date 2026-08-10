/* Sunday Ready Platform v1.5.4 - Licensing Foundation */
IF OBJECT_ID('dbo.ApplicationLicense', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationLicense
    (
        Id int NOT NULL CONSTRAINT PK_ApplicationLicense PRIMARY KEY,
        ProtectedLicenseKey nvarchar(max) NULL,
        LicenseStatus nvarchar(50) NOT NULL CONSTRAINT DF_ApplicationLicense_Status DEFAULT('NotActivated'),
        CustomerName nvarchar(250) NULL,
        ProductName nvarchar(250) NULL,
        LicensedVersion nvarchar(50) NULL,
        ExpirationDate datetimeoffset NULL,
        LicensedModules nvarchar(max) NULL,
        LastValidatedUtc datetime2 NULL,
        UpdatedUtc datetime2 NOT NULL CONSTRAINT DF_ApplicationLicense_UpdatedUtc DEFAULT(SYSUTCDATETIME())
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationLicense WHERE Id = 1)
    INSERT INTO dbo.ApplicationLicense (Id, LicenseStatus) VALUES (1, 'NotActivated');
