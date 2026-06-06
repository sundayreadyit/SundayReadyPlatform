USE ChurchAssetTracker;
GO

IF COL_LENGTH('dbo.Reservations', 'RecurrenceGroupId') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations ADD RecurrenceGroupId UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('dbo.Reservations', 'ParentReservationId') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations ADD ParentReservationId INT NULL;
END
GO

IF COL_LENGTH('dbo.Reservations', 'IsRecurring') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations ADD IsRecurring BIT NOT NULL CONSTRAINT DF_Reservations_IsRecurring DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Reservations', 'RecurrencePattern') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations ADD RecurrencePattern NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.Reservations', 'RecurrenceEndDate') IS NULL
BEGIN
    ALTER TABLE dbo.Reservations ADD RecurrenceEndDate DATE NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Reservations_RecurrenceGroupId'
      AND object_id = OBJECT_ID('dbo.Reservations')
)
BEGIN
    CREATE INDEX IX_Reservations_RecurrenceGroupId
    ON dbo.Reservations (RecurrenceGroupId);
END
GO

SELECT 'Recurring reservations update complete' AS Result;
GO