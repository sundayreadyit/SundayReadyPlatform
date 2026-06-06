USE ChurchAssetTracker;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'KeyManager')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('KeyManager', 'Can manage keys and key access areas');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AccessAreas WHERE AreaName = 'Main Building')
BEGIN
    INSERT INTO dbo.AccessAreas (AreaName, Description, IsActive)
    VALUES ('Main Building', 'Primary church building', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AccessAreas WHERE AreaName = 'Sanctuary')
BEGIN
    INSERT INTO dbo.AccessAreas (AreaName, Description, IsActive)
    VALUES ('Sanctuary', 'Main worship area', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AccessAreas WHERE AreaName = 'Office')
BEGIN
    INSERT INTO dbo.AccessAreas (AreaName, Description, IsActive)
    VALUES ('Office', 'General church office area', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AccessAreas WHERE AreaName = 'Storage')
BEGIN
    INSERT INTO dbo.AccessAreas (AreaName, Description, IsActive)
    VALUES ('Storage', 'General storage area', 1);
END
GO

SELECT * FROM dbo.AccessAreas ORDER BY AreaName;
GO