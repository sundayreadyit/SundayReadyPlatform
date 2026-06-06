USE ChurchAssetTracker;
GO

-- Default first admin account
-- Username: admin
-- Password: ChangeMe123!
-- IMPORTANT: Change this password after first login by adding a password-change page or replacing this account later.

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, DisplayName, Email, PasswordHash, IsActive)
    VALUES ('admin', 'System Administrator', NULL, 'PBKDF2-SHA256$100000$AQIDBAUGBwgJCgsMDQ4PEA==$4GqoH/SqHM86aKvYpt8G51CnwfVCN1AY5DjzvwZFMtI=', 1);
END
ELSE
BEGIN
    UPDATE dbo.Users
    SET DisplayName = 'System Administrator',
        PasswordHash = 'PBKDF2-SHA256$100000$AQIDBAUGBwgJCgsMDQ4PEA==$4GqoH/SqHM86aKvYpt8G51CnwfVCN1AY5DjzvwZFMtI=',
        IsActive = 1
    WHERE Username = 'admin';
END
GO

DECLARE @AdminUserId INT = (SELECT UserId FROM dbo.Users WHERE Username = 'admin');
DECLARE @AdminRoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Admin');

IF @AdminRoleId IS NULL
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('Admin', 'Full system access');
    SET @AdminRoleId = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @AdminUserId AND RoleId = @AdminRoleId)
BEGIN
    INSERT INTO dbo.UserRoles (UserId, RoleId)
    VALUES (@AdminUserId, @AdminRoleId);
END
GO

SELECT u.UserId, u.Username, u.DisplayName, r.RoleName
FROM dbo.Users u
LEFT JOIN dbo.UserRoles ur ON u.UserId = ur.UserId
LEFT JOIN dbo.Roles r ON ur.RoleId = r.RoleId
WHERE u.Username = 'admin';
GO
