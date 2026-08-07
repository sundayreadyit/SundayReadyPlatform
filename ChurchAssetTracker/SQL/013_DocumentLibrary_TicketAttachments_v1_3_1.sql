USE ChurchAssetTracker;
GO

/*
    CWC Operations Portal v1.3.1
    Documentation Library & IT Support Ticket Attachments

    Notes:
    - Documentation Library reads files directly from the configured share and does not require a SQL table.
    - Ticket attachments and ticket comment/note attachments require the two tables below.
*/

IF OBJECT_ID('dbo.ITSupportTicketAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ITSupportTicketAttachments (
        AttachmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ITSupportTicketAttachments PRIMARY KEY,
        TicketId INT NOT NULL,
        OriginalFileName NVARCHAR(260) NOT NULL,
        StoredFileName NVARCHAR(260) NOT NULL,
        FilePath NVARCHAR(1000) NOT NULL,
        ContentType NVARCHAR(200) NULL,
        FileSizeBytes BIGINT NOT NULL,
        UploadedByUserId INT NULL,
        UploadedDate DATETIME2 NOT NULL CONSTRAINT DF_ITSupportTicketAttachments_UploadedDate DEFAULT (SYSDATETIME()),
        CONSTRAINT FK_ITSupportTicketAttachments_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.ITSupportTickets(TicketId),
        CONSTRAINT FK_ITSupportTicketAttachments_User FOREIGN KEY (UploadedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ITSupportTicketAttachments')
      AND name = 'IX_ITSupportTicketAttachments_TicketId'
)
BEGIN
    CREATE INDEX IX_ITSupportTicketAttachments_TicketId
        ON dbo.ITSupportTicketAttachments (TicketId, UploadedDate DESC);
END
GO

IF OBJECT_ID('dbo.ITSupportTicketCommentAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ITSupportTicketCommentAttachments (
        CommentAttachmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ITSupportTicketCommentAttachments PRIMARY KEY,
        CommentId INT NOT NULL,
        OriginalFileName NVARCHAR(260) NOT NULL,
        StoredFileName NVARCHAR(260) NOT NULL,
        FilePath NVARCHAR(1000) NOT NULL,
        ContentType NVARCHAR(200) NULL,
        FileSizeBytes BIGINT NOT NULL,
        UploadedByUserId INT NULL,
        UploadedDate DATETIME2 NOT NULL CONSTRAINT DF_ITSupportTicketCommentAttachments_UploadedDate DEFAULT (SYSDATETIME()),
        CONSTRAINT FK_ITSupportTicketCommentAttachments_Comment FOREIGN KEY (CommentId) REFERENCES dbo.ITSupportTicketComments(CommentId),
        CONSTRAINT FK_ITSupportTicketCommentAttachments_User FOREIGN KEY (UploadedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ITSupportTicketCommentAttachments')
      AND name = 'IX_ITSupportTicketCommentAttachments_CommentId'
)
BEGIN
    CREATE INDEX IX_ITSupportTicketCommentAttachments_CommentId
        ON dbo.ITSupportTicketCommentAttachments (CommentId, UploadedDate DESC);
END
GO

SELECT 'v1.3.1 Documentation Library and Ticket Attachments schema verified.' AS Result;
GO
