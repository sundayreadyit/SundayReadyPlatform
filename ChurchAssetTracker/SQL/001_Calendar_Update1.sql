USE ChurchAssetTracker;
GO

IF COL_LENGTH('dbo.AccessAreas', 'CalendarColor') IS NULL
BEGIN
    ALTER TABLE dbo.AccessAreas
    ADD CalendarColor NVARCHAR(20) NULL;
END
GO

;WITH ColorDefaults AS (
    SELECT AccessAreaId,
           CASE ((ROW_NUMBER() OVER (ORDER BY AreaName) - 1) % 10)
                WHEN 0 THEN '#2563eb'
                WHEN 1 THEN '#16a34a'
                WHEN 2 THEN '#9333ea'
                WHEN 3 THEN '#ea580c'
                WHEN 4 THEN '#0891b2'
                WHEN 5 THEN '#be123c'
                WHEN 6 THEN '#4f46e5'
                WHEN 7 THEN '#15803d'
                WHEN 8 THEN '#a16207'
                ELSE '#475569'
           END AS DefaultColor
    FROM dbo.AccessAreas
)
UPDATE aa
SET CalendarColor = cd.DefaultColor
FROM dbo.AccessAreas aa
JOIN ColorDefaults cd ON aa.AccessAreaId = cd.AccessAreaId
WHERE aa.CalendarColor IS NULL OR LTRIM(RTRIM(aa.CalendarColor)) = '';
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'ReservationManager')
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description)
    VALUES ('ReservationManager', 'Can manage church building and room reservations');
END
GO

SELECT AccessAreaId, AreaName, CalendarColor
FROM dbo.AccessAreas
ORDER BY AreaName;
GO