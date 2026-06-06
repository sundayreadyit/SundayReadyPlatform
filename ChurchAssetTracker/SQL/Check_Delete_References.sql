USE ChurchAssetTracker;
GO

-- Change these IDs before running.
DECLARE @UserId INT = 1;
DECLARE @PersonId INT = 1;
DECLARE @StudentId INT = 1;
DECLARE @FacultyStaffId INT = 1;

SELECT 'UserRoles' AS TableName, COUNT(*) AS ReferenceCount FROM dbo.UserRoles WHERE UserId = @UserId
UNION ALL SELECT 'AuditLog by UserId', COUNT(*) FROM dbo.AuditLog WHERE UserId = @UserId
UNION ALL SELECT 'ITSupportTickets AssignedTo', COUNT(*) FROM dbo.ITSupportTickets WHERE AssignedToUserId = @UserId
UNION ALL SELECT 'ITSupportTickets CreatedBy', COUNT(*) FROM dbo.ITSupportTickets WHERE CreatedByUserId = @UserId
UNION ALL SELECT 'ITSupportTicketComments', COUNT(*) FROM dbo.ITSupportTicketComments WHERE CreatedByUserId = @UserId
UNION ALL SELECT 'UserPasswordSetupTokens', COUNT(*) FROM dbo.UserPasswordSetupTokens WHERE UserId = @UserId;

SELECT 'AssetCheckouts PersonId' AS TableName, COUNT(*) AS ReferenceCount FROM dbo.AssetCheckouts WHERE PersonId = @PersonId
UNION ALL SELECT 'KeyAssignments PersonId', COUNT(*) FROM dbo.KeyAssignments WHERE PersonId = @PersonId
UNION ALL SELECT 'ITSupportTickets RequestedByPersonId', COUNT(*) FROM dbo.ITSupportTickets WHERE RequestedByPersonId = @PersonId;

SELECT 'Students direct record' AS TableName, COUNT(*) AS ReferenceCount FROM dbo.Students WHERE StudentId = @StudentId;

SELECT 'FacultyStaff direct record' AS TableName, COUNT(*) AS ReferenceCount FROM dbo.FacultyStaff WHERE FacultyStaffId = @FacultyStaffId;
GO