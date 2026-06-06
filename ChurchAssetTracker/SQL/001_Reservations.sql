
USE ChurchAssetTracker;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ReservationManager')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ReservationManager', 'Can manage church building and room reservations');
END
GO

IF OBJECT_ID('dbo.Reservations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reservations (
        ReservationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EventName NVARCHAR(200) NOT NULL,
        RequestedByPersonId INT NULL,
        AccessAreaId INT NULL,
        StartDateTime DATETIME2 NOT NULL,
        EndDateTime DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_Reservations_Status DEFAULT ('Pending'),
        Purpose NVARCHAR(MAX) NULL,
        SetupNotes NVARCHAR(MAX) NULL,
        AccessKeyNeeds NVARCHAR(MAX) NULL,
        ContactName NVARCHAR(150) NULL,
        ContactPhone NVARCHAR(50) NULL,
        ContactEmail NVARCHAR(255) NULL,
        IsPublicEvent BIT NOT NULL CONSTRAINT DF_Reservations_IsPublicEvent DEFAULT (0),
        Notes NVARCHAR(MAX) NULL,
        ApprovedByUserId INT NULL,
        ApprovedDate DATETIME2 NULL,
        CreatedByUserId INT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Reservations_CreatedDate DEFAULT (SYSDATETIME()),
        UpdatedDate DATETIME2 NULL,
        CONSTRAINT FK_Reservations_People FOREIGN KEY (RequestedByPersonId) REFERENCES dbo.People(PersonId),
        CONSTRAINT FK_Reservations_AccessAreas FOREIGN KEY (AccessAreaId) REFERENCES dbo.AccessAreas(AccessAreaId),
        CONSTRAINT FK_Reservations_ApprovedByUser FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Reservations_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservations_AccessArea_Start_End' AND object_id = OBJECT_ID('dbo.Reservations'))
BEGIN
    CREATE INDEX IX_Reservations_AccessArea_Start_End ON dbo.Reservations (AccessAreaId, StartDateTime, EndDateTime, Status);
END
GO
