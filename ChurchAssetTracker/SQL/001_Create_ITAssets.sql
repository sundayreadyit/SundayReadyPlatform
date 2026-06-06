USE ChurchAssetTracker;
GO

IF COL_LENGTH('dbo.Users', 'UserId') IS NULL
BEGIN
    RAISERROR('Expected dbo.Users table was not found.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ITAssetManager')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ITAssetManager', 'Can manage restricted IT asset records');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ITAssetViewer')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ITAssetViewer', 'Can view restricted IT asset records');
END
GO

IF OBJECT_ID('dbo.ITAssets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ITAssets (
        ITAssetId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AssetName NVARCHAR(150) NOT NULL,
        AssetType NVARCHAR(100) NULL,
        Make NVARCHAR(100) NULL,
        Model NVARCHAR(100) NULL,
        SerialNumber NVARCHAR(100) NULL,
        AssetTag NVARCHAR(100) NULL,
        LoginUsername NVARCHAR(150) NULL,
        CredentialReference NVARCHAR(255) NULL,
        IPAddress NVARCHAR(50) NULL,
        MACAddress NVARCHAR(50) NULL,
        Location NVARCHAR(150) NULL,
        AssignedTo NVARCHAR(150) NULL,
        OperatingSystem NVARCHAR(150) NULL,
        PurchaseDate DATE NULL,
        WarrantyExpiration DATE NULL,
        Notes NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ITAssets_IsActive DEFAULT (1),
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ITAssets_CreatedDate DEFAULT (SYSDATETIME()),
        ModifiedDate DATETIME2 NULL
    );
END
GO

-- Helpful indexes for searching/filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ITAssets_AssetName' AND object_id = OBJECT_ID('dbo.ITAssets'))
    CREATE INDEX IX_ITAssets_AssetName ON dbo.ITAssets (AssetName);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ITAssets_IPAddress' AND object_id = OBJECT_ID('dbo.ITAssets'))
    CREATE INDEX IX_ITAssets_IPAddress ON dbo.ITAssets (IPAddress);
GO

SELECT * FROM dbo.Roles WHERE RoleName IN ('ITAssetManager', 'ITAssetViewer');
SELECT TOP 10 * FROM dbo.ITAssets;
GO
