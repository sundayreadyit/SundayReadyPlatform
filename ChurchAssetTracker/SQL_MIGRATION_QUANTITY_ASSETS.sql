USE ChurchAssetTracker;
GO

IF COL_LENGTH('dbo.Assets', 'TotalQuantity') IS NULL
BEGIN
    ALTER TABLE dbo.Assets
    ADD TotalQuantity INT NOT NULL CONSTRAINT DF_Assets_TotalQuantity DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.AssetCheckouts', 'QuantityOut') IS NULL
BEGIN
    ALTER TABLE dbo.AssetCheckouts
    ADD QuantityOut INT NOT NULL CONSTRAINT DF_AssetCheckouts_QuantityOut DEFAULT (1);
END
GO

UPDATE dbo.Assets
SET TotalQuantity = 1
WHERE TotalQuantity IS NULL OR TotalQuantity < 1;
GO

UPDATE dbo.AssetCheckouts
SET QuantityOut = 1
WHERE QuantityOut IS NULL OR QuantityOut < 1;
GO

SELECT AssetName, TotalQuantity
FROM dbo.Assets
ORDER BY AssetName;
GO
