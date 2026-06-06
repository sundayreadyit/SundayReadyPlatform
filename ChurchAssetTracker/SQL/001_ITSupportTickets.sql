USE ChurchAssetTracker;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ITSupportManager')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ITSupportManager', 'Can manage IT support tickets');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ITSupportTech')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ITSupportTech', 'Can work assigned IT support tickets');
END
GO

IF OBJECT_ID('dbo.ITSupportTickets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ITSupportTickets (
        TicketId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

        TicketNumber NVARCHAR(30) NULL,

        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,

        Category NVARCHAR(100) NULL,
        Priority NVARCHAR(50) NOT NULL CONSTRAINT DF_ITSupportTickets_Priority DEFAULT ('Medium'),
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_ITSupportTickets_Status DEFAULT ('New'),

        RequestedByPersonId INT NULL,
        RequestedByName NVARCHAR(150) NULL,
        RequestedByEmail NVARCHAR(255) NULL,
        RequestedByPhone NVARCHAR(50) NULL,

        AssignedToUserId INT NULL,

        ITAssetId INT NULL,
        AccessAreaId INT NULL,

        DueDate DATETIME2 NULL,

        CreatedByUserId INT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ITSupportTickets_CreatedDate DEFAULT (SYSDATETIME()),

        UpdatedDate DATETIME2 NULL,
        ResolvedDate DATETIME2 NULL,
        ClosedDate DATETIME2 NULL,

        CONSTRAINT FK_ITSupportTickets_RequestedByPerson
            FOREIGN KEY (RequestedByPersonId) REFERENCES dbo.People(PersonId),

        CONSTRAINT FK_ITSupportTickets_AssignedToUser
            FOREIGN KEY (AssignedToUserId) REFERENCES dbo.Users(UserId),

        CONSTRAINT FK_ITSupportTickets_AccessAreas
            FOREIGN KEY (AccessAreaId) REFERENCES dbo.AccessAreas(AccessAreaId),

        CONSTRAINT FK_ITSupportTickets_CreatedByUser
            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF OBJECT_ID('dbo.ITSupportTicketComments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ITSupportTicketComments (
        CommentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TicketId INT NOT NULL,
        CommentText NVARCHAR(MAX) NOT NULL,
        IsInternal BIT NOT NULL CONSTRAINT DF_ITSupportTicketComments_IsInternal DEFAULT (0),
        CreatedByUserId INT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ITSupportTicketComments_CreatedDate DEFAULT (SYSDATETIME()),

        CONSTRAINT FK_ITSupportTicketComments_Tickets
            FOREIGN KEY (TicketId) REFERENCES dbo.ITSupportTickets(TicketId),

        CONSTRAINT FK_ITSupportTicketComments_CreatedByUser
            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ITSupportTickets_Status_Priority'
      AND object_id = OBJECT_ID('dbo.ITSupportTickets')
)
BEGIN
    CREATE INDEX IX_ITSupportTickets_Status_Priority
    ON dbo.ITSupportTickets (Status, Priority, CreatedDate);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ITSupportTicketComments_TicketId'
      AND object_id = OBJECT_ID('dbo.ITSupportTicketComments')
)
BEGIN
    CREATE INDEX IX_ITSupportTicketComments_TicketId
    ON dbo.ITSupportTicketComments (TicketId, CreatedDate);
END
GO

SELECT 'IT Support Tickets setup complete' AS Result;
GO