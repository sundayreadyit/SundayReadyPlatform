USE ChurchAssetTracker;
GO

SELECT DISTINCT Status
FROM dbo.ITSupportTickets
ORDER BY Status;
GO