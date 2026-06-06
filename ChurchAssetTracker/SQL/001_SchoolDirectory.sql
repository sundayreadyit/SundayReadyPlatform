USE ChurchAssetTracker;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'SchoolAdmin')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('SchoolAdmin', 'Can manage school directory records');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'SchoolStaff')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('SchoolStaff', 'Can access school directory records');
END
GO

IF OBJECT_ID('dbo.Students', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Students (
        StudentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        PreferredName NVARCHAR(100) NULL,
        DateOfBirth DATE NULL,
        GradeLevel NVARCHAR(50) NULL,
        Classroom NVARCHAR(100) NULL,
        ParentGuardian1Name NVARCHAR(150) NULL,
        ParentGuardian1Phone NVARCHAR(50) NULL,
        ParentGuardian1Email NVARCHAR(255) NULL,
        ParentGuardian2Name NVARCHAR(150) NULL,
        ParentGuardian2Phone NVARCHAR(50) NULL,
        ParentGuardian2Email NVARCHAR(255) NULL,
        EmergencyContactName NVARCHAR(150) NULL,
        EmergencyContactPhone NVARCHAR(50) NULL,
        EmergencyContactRelationship NVARCHAR(100) NULL,
        AllergiesMedicalNotes NVARCHAR(MAX) NULL,
        AuthorizedPickupNotes NVARCHAR(MAX) NULL,
        Notes NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Students_IsActive DEFAULT (1),
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Students_CreatedDate DEFAULT (SYSDATETIME()),
        ModifiedDate DATETIME2 NULL
    );
END
GO

IF OBJECT_ID('dbo.FacultyStaff', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacultyStaff (
        FacultyStaffId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        PreferredName NVARCHAR(100) NULL,
        RoleTitle NVARCHAR(150) NULL,
        Department NVARCHAR(100) NULL,
        Classroom NVARCHAR(100) NULL,
        Phone NVARCHAR(50) NULL,
        Email NVARCHAR(255) NULL,
        EmergencyContactName NVARCHAR(150) NULL,
        EmergencyContactPhone NVARCHAR(50) NULL,
        EmergencyContactRelationship NVARCHAR(100) NULL,
        Notes NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_FacultyStaff_IsActive DEFAULT (1),
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_FacultyStaff_CreatedDate DEFAULT (SYSDATETIME()),
        ModifiedDate DATETIME2 NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Students_Name' AND object_id = OBJECT_ID('dbo.Students'))
BEGIN
    CREATE INDEX IX_Students_Name ON dbo.Students (LastName, FirstName, IsActive);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacultyStaff_Name' AND object_id = OBJECT_ID('dbo.FacultyStaff'))
BEGIN
    CREATE INDEX IX_FacultyStaff_Name ON dbo.FacultyStaff (LastName, FirstName, IsActive);
END
GO

SELECT 'School Directory setup complete' AS Result;
GO