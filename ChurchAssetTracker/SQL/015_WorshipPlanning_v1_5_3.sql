/* Sunday Ready Platform v1.5.3 - Worship Planning
   Optional manual migration. The application also creates these tables on first use. */

IF OBJECT_ID('dbo.WorshipSets','U') IS NULL
BEGIN
    CREATE TABLE dbo.WorshipSets(
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ServiceDate DATE NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Notes NVARCHAR(4000) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_WorshipSets_Status DEFAULT('Draft'),
        CreatedBy NVARCHAR(200) NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_WorshipSets_CreatedDate DEFAULT(SYSDATETIME()),
        UpdatedDate DATETIME2 NOT NULL CONSTRAINT DF_WorshipSets_UpdatedDate DEFAULT(SYSDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.WorshipSetItems','U') IS NULL
BEGIN
    CREATE TABLE dbo.WorshipSetItems(
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorshipSetId INT NOT NULL,
        SortOrder INT NOT NULL,
        SongTitle NVARCHAR(300) NOT NULL,
        RelativePath NVARCHAR(1000) NOT NULL,
        KeyOverride NVARCHAR(30) NULL,
        Leader NVARCHAR(200) NULL,
        Notes NVARCHAR(2000) NULL,
        CONSTRAINT FK_WorshipSetItems_WorshipSets FOREIGN KEY(WorshipSetId) REFERENCES dbo.WorshipSets(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_WorshipSetItems_Set_Order ON dbo.WorshipSetItems(WorshipSetId, SortOrder);
    CREATE INDEX IX_WorshipSetItems_Path ON dbo.WorshipSetItems(RelativePath);
END;
GO


IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'WorshipLeader')
BEGIN
    INSERT INTO dbo.Roles(RoleName, Description) VALUES('WorshipLeader', 'Access to Worship Planning, song library, service sets, and worship packets');
END;
GO
