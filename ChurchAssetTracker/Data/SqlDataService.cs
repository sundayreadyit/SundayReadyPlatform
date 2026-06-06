using System.Data;
using Microsoft.Data.SqlClient;
using ChurchAssetTracker.Models;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    private readonly string _connectionString;

    public SqlDataService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task LogAuditAsync(string actionType, string entityType, int? entityId, string? description, int? userId = null)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
                             VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Description)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLogRow>> GetAuditLogsAsync(int take = 200)
    {
        var list = new List<AuditLogRow>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT TOP (@Take) al.AuditLogId, al.ActionType, al.EntityType, al.EntityId, al.Description, al.CreatedDate, u.DisplayName
                             FROM dbo.AuditLog al
                             LEFT JOIN dbo.Users u ON al.UserId = u.UserId
                             ORDER BY al.CreatedDate DESC, al.AuditLogId DESC";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AuditLogRow
            {
                AuditLogId = r.GetInt32(0),
                ActionType = r.GetString(1),
                EntityType = r.GetString(2),
                EntityId = r.IsDBNull(3) ? null : r.GetInt32(3),
                Description = r.IsDBNull(4) ? null : r.GetString(4),
                CreatedDate = r.GetDateTime(5),
                UserDisplayName = r.IsDBNull(6) ? null : r.GetString(6)
            });
        }
        return list;
    }

    public async Task<int> CountAsync(string sql)
    {
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        return new DashboardViewModel
        {
            PeopleCount = await CountAsync("SELECT COUNT(*) FROM dbo.People WHERE IsActive = 1"),
            AssetCount = await CountAsync("SELECT COALESCE(SUM(TotalQuantity), 0) FROM dbo.Assets WHERE IsActive = 1"),
            CheckedOutItems = await CountAsync("SELECT COALESCE(SUM(QuantityOut), 0) FROM dbo.AssetCheckouts WHERE ActualReturnDate IS NULL"),
            OverdueItems = await CountAsync("SELECT COUNT(*) FROM dbo.AssetCheckouts WHERE ActualReturnDate IS NULL AND ExpectedReturnDate < SYSDATETIME()"),
            KeysIssued = await CountAsync("SELECT COUNT(*) FROM dbo.KeyAssignments WHERE ReturnedDate IS NULL"),
            LostKeys = await CountAsync("SELECT COUNT(*) FROM dbo.KeyAssignments WHERE Status = 'Lost'")
        };
    }

    public async Task<List<PersonRow>> GetPeopleAsync()
    {
        var list = new List<PersonRow>();
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand("SELECT PersonId, FirstName + ' ' + LastName AS FullName, Phone, Email, MinistryTeam, IsActive FROM dbo.People ORDER BY LastName, FirstName", conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new PersonRow { PersonId = r.GetInt32(0), FullName = r.GetString(1), Phone = r.IsDBNull(2) ? null : r.GetString(2), Email = r.IsDBNull(3) ? null : r.GetString(3), MinistryTeam = r.IsDBNull(4) ? null : r.GetString(4), IsActive = r.GetBoolean(5) });
        return list;
    }


    public async Task<PersonEditViewModel?> GetPersonForEditAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT PersonId, FirstName, LastName, Phone, Email, MinistryTeam, IsActive, Notes
                             FROM dbo.People WHERE PersonId = @PersonId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PersonId", id);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new PersonEditViewModel
        {
            PersonId = r.GetInt32(0),
            FirstName = r.GetString(1),
            LastName = r.GetString(2),
            Phone = r.IsDBNull(3) ? null : r.GetString(3),
            Email = r.IsDBNull(4) ? null : r.GetString(4),
            MinistryTeam = r.IsDBNull(5) ? null : r.GetString(5),
            IsActive = r.GetBoolean(6),
            Notes = r.IsDBNull(7) ? null : r.GetString(7)
        };
    }

    public async Task CreatePersonAsync(PersonEditViewModel person)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.People (FirstName, LastName, Phone, Email, MinistryTeam, IsActive, Notes)
                             VALUES (@FirstName, @LastName, @Phone, @Email, @MinistryTeam, @IsActive, @Notes)";
        await using var cmd = new SqlCommand(sql, conn);
        AddPersonParameters(cmd, person);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePersonAsync(PersonEditViewModel person)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.People
                             SET FirstName = @FirstName,
                                 LastName = @LastName,
                                 Phone = @Phone,
                                 Email = @Email,
                                 MinistryTeam = @MinistryTeam,
                                 IsActive = @IsActive,
                                 Notes = @Notes
                             WHERE PersonId = @PersonId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PersonId", person.PersonId);
        AddPersonParameters(cmd, person);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeactivatePersonAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = "UPDATE dbo.People SET IsActive = 0 WHERE PersonId = @PersonId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PersonId", id);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddPersonParameters(SqlCommand cmd, PersonEditViewModel person)
    {
        cmd.Parameters.AddWithValue("@FirstName", person.FirstName.Trim());
        cmd.Parameters.AddWithValue("@LastName", person.LastName.Trim());
        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(person.Phone) ? DBNull.Value : person.Phone.Trim());
        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(person.Email) ? DBNull.Value : person.Email.Trim());
        cmd.Parameters.AddWithValue("@MinistryTeam", string.IsNullOrWhiteSpace(person.MinistryTeam) ? DBNull.Value : person.MinistryTeam.Trim());
        cmd.Parameters.AddWithValue("@IsActive", person.IsActive);
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(person.Notes) ? DBNull.Value : person.Notes.Trim());
    }

    public async Task<List<AssetRow>> GetAssetsAsync()
    {
        var list = new List<AssetRow>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT a.AssetId, a.AssetName, a.PhotoPath, a.Category, a.AssetTag, a.SerialNumber, a.CurrentCondition, a.TotalQuantity,
                                    COALESCE(SUM(CASE WHEN ac.ActualReturnDate IS NULL THEN ac.QuantityOut ELSE 0 END), 0) AS QuantityCheckedOut,
                                    a.TotalQuantity - COALESCE(SUM(CASE WHEN ac.ActualReturnDate IS NULL THEN ac.QuantityOut ELSE 0 END), 0) AS QuantityAvailable,
                                    a.IsActive
                             FROM dbo.Assets a
                             LEFT JOIN dbo.AssetCheckouts ac ON a.AssetId = ac.AssetId
                             GROUP BY a.AssetId, a.AssetName, a.PhotoPath, a.Category, a.AssetTag, a.SerialNumber, a.CurrentCondition, a.TotalQuantity, a.IsActive
                             ORDER BY a.AssetName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new AssetRow { AssetId = r.GetInt32(0), AssetName = r.GetString(1), PhotoPath = r.IsDBNull(2) ? null : r.GetString(2), Category = r.IsDBNull(3) ? null : r.GetString(3), AssetTag = r.IsDBNull(4) ? null : r.GetString(4), SerialNumber = r.IsDBNull(5) ? null : r.GetString(5), CurrentCondition = r.IsDBNull(6) ? null : r.GetString(6), TotalQuantity = r.GetInt32(7), QuantityCheckedOut = r.GetInt32(8), QuantityAvailable = r.GetInt32(9), IsActive = r.GetBoolean(10) });
        return list;
    }


    public async Task<AssetEditViewModel?> GetAssetForEditAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT AssetId, AssetName, PhotoPath, Category, AssetTag, SerialNumber, Description, TotalQuantity, CurrentCondition, IsActive
                             FROM dbo.Assets WHERE AssetId = @AssetId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetId", id);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new AssetEditViewModel
        {
            AssetId = r.GetInt32(0),
            AssetName = r.GetString(1),
            PhotoPath = r.IsDBNull(2) ? null : r.GetString(2),
            Category = r.IsDBNull(3) ? null : r.GetString(3),
            AssetTag = r.IsDBNull(4) ? null : r.GetString(4),
            SerialNumber = r.IsDBNull(5) ? null : r.GetString(5),
            Description = r.IsDBNull(6) ? null : r.GetString(6),
            TotalQuantity = r.GetInt32(7),
            CurrentCondition = r.IsDBNull(8) ? null : r.GetString(8),
            IsActive = r.GetBoolean(9)
        };
    }

    public async Task CreateAssetAsync(AssetEditViewModel asset)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.Assets (AssetName, PhotoPath, Category, AssetTag, SerialNumber, Description, TotalQuantity, CurrentCondition, IsActive)
                             VALUES (@AssetName, @PhotoPath, @Category, @AssetTag, @SerialNumber, @Description, @TotalQuantity, @CurrentCondition, @IsActive)";
        await using var cmd = new SqlCommand(sql, conn);
        AddAssetParameters(cmd, asset);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAssetAsync(AssetEditViewModel asset)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.Assets
                             SET AssetName = @AssetName,
                                 PhotoPath = @PhotoPath,
                                 Category = @Category,
                                 AssetTag = @AssetTag,
                                 SerialNumber = @SerialNumber,
                                 Description = @Description,
                                 TotalQuantity = @TotalQuantity,
                                 CurrentCondition = @CurrentCondition,
                                 IsActive = @IsActive
                             WHERE AssetId = @AssetId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetId", asset.AssetId);
        AddAssetParameters(cmd, asset);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeactivateAssetAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = "UPDATE dbo.Assets SET IsActive = 0 WHERE AssetId = @AssetId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetId", id);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddAssetParameters(SqlCommand cmd, AssetEditViewModel asset)
    {
        cmd.Parameters.AddWithValue("@AssetName", asset.AssetName.Trim());
        cmd.Parameters.AddWithValue("@PhotoPath", string.IsNullOrWhiteSpace(asset.PhotoPath) ? DBNull.Value : asset.PhotoPath.Trim());
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(asset.Category) ? DBNull.Value : asset.Category.Trim());
        cmd.Parameters.AddWithValue("@AssetTag", string.IsNullOrWhiteSpace(asset.AssetTag) ? DBNull.Value : asset.AssetTag.Trim());
        cmd.Parameters.AddWithValue("@SerialNumber", string.IsNullOrWhiteSpace(asset.SerialNumber) ? DBNull.Value : asset.SerialNumber.Trim());
        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(asset.Description) ? DBNull.Value : asset.Description.Trim());
        cmd.Parameters.AddWithValue("@TotalQuantity", asset.TotalQuantity < 1 ? 1 : asset.TotalQuantity);
        cmd.Parameters.AddWithValue("@CurrentCondition", string.IsNullOrWhiteSpace(asset.CurrentCondition) ? DBNull.Value : asset.CurrentCondition.Trim());
        cmd.Parameters.AddWithValue("@IsActive", asset.IsActive);
    }

    public async Task<List<KeyRow>> GetKeysAsync()
    {
        var list = new List<KeyRow>();
        await using var conn = CreateConnection();
        const string sql = @"SELECT k.KeyId, k.KeyCode, k.KeyName, k.IsMasterKey, k.IsActive,
                                    COALESCE(STRING_AGG(aa.AreaName, ', '), '') AS AccessAreas
                             FROM dbo.Keys k
                             LEFT JOIN dbo.KeyAccessAreas kaa ON k.KeyId = kaa.KeyId
                             LEFT JOIN dbo.AccessAreas aa ON kaa.AccessAreaId = aa.AccessAreaId
                             GROUP BY k.KeyId, k.KeyCode, k.KeyName, k.IsMasterKey, k.IsActive
                             ORDER BY k.KeyName";
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new KeyRow { KeyId = r.GetInt32(0), KeyCode = r.GetString(1), KeyName = r.GetString(2), IsMasterKey = r.GetBoolean(3), IsActive = r.GetBoolean(4), AccessAreas = r.IsDBNull(5) ? null : r.GetString(5) });
        return list;
    }


    public async Task<KeyEditViewModel?> GetKeyForEditAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = @"SELECT KeyId, KeyCode, KeyName, Description, IsMasterKey, IsActive
                             FROM dbo.Keys
                             WHERE KeyId = @KeyId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyId", id);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new KeyEditViewModel
        {
            KeyId = r.GetInt32(0),
            KeyCode = r.GetString(1),
            KeyName = r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            IsMasterKey = r.GetBoolean(4),
            IsActive = r.GetBoolean(5)
        };
    }

    public async Task CreateKeyAsync(KeyEditViewModel key)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.Keys (KeyCode, KeyName, Description, IsMasterKey, IsActive)
                             VALUES (@KeyCode, @KeyName, @Description, @IsMasterKey, @IsActive)";
        await using var cmd = new SqlCommand(sql, conn);
        AddKeyParameters(cmd, key);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateKeyAsync(KeyEditViewModel key)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.Keys
                             SET KeyCode = @KeyCode,
                                 KeyName = @KeyName,
                                 Description = @Description,
                                 IsMasterKey = @IsMasterKey,
                                 IsActive = @IsActive
                             WHERE KeyId = @KeyId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyId", key.KeyId);
        AddKeyParameters(cmd, key);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeactivateKeyAsync(int id)
    {
        await using var conn = CreateConnection();
        const string sql = "UPDATE dbo.Keys SET IsActive = 0 WHERE KeyId = @KeyId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyId", id);
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddKeyParameters(SqlCommand cmd, KeyEditViewModel key)
    {
        cmd.Parameters.AddWithValue("@KeyCode", key.KeyCode.Trim());
        cmd.Parameters.AddWithValue("@KeyName", key.KeyName.Trim());
        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(key.Description) ? DBNull.Value : key.Description.Trim());
        cmd.Parameters.AddWithValue("@IsMasterKey", key.IsMasterKey);
        cmd.Parameters.AddWithValue("@IsActive", key.IsActive);
    }

    public async Task<List<CheckoutRow>> GetCheckoutsAsync()
    {
        var list = new List<CheckoutRow>();
        const string sql = @"SELECT ac.CheckoutId, a.AssetName, p.FirstName + ' ' + p.LastName AS Borrower, ac.QuantityOut, ac.CheckoutDate, ac.ExpectedReturnDate, ac.ActualReturnDate, ac.Status
                             FROM dbo.AssetCheckouts ac JOIN dbo.Assets a ON ac.AssetId = a.AssetId JOIN dbo.People p ON ac.PersonId = p.PersonId ORDER BY ac.CheckoutDate DESC";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new CheckoutRow { CheckoutId = r.GetInt32(0), AssetName = r.GetString(1), Borrower = r.GetString(2), QuantityOut = r.GetInt32(3), CheckoutDate = r.GetDateTime(4), ExpectedReturnDate = r.IsDBNull(5) ? null : r.GetDateTime(5), ActualReturnDate = r.IsDBNull(6) ? null : r.GetDateTime(6), Status = r.GetString(7) });
        return list;
    }


    public async Task<List<OptionItem>> GetActivePeopleOptionsAsync()
    {
        var list = new List<OptionItem>();
        const string sql = @"SELECT PersonId, FirstName + ' ' + LastName AS FullName
                             FROM dbo.People
                             WHERE IsActive = 1
                             ORDER BY LastName, FirstName";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OptionItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }

    public async Task<List<OptionItem>> GetAvailableAssetOptionsAsync()
    {
        var list = new List<OptionItem>();
        const string sql = @"SELECT a.AssetId,
                                    a.AssetName + COALESCE(' - ' + NULLIF(a.AssetTag, ''), '') + ' (Available: ' + CAST((a.TotalQuantity - COALESCE(SUM(CASE WHEN ac.ActualReturnDate IS NULL THEN ac.QuantityOut ELSE 0 END), 0)) AS varchar(20)) + ')' AS DisplayName
                             FROM dbo.Assets a
                             LEFT JOIN dbo.AssetCheckouts ac ON a.AssetId = ac.AssetId
                             WHERE a.IsActive = 1
                             GROUP BY a.AssetId, a.AssetName, a.AssetTag, a.TotalQuantity
                             HAVING a.TotalQuantity - COALESCE(SUM(CASE WHEN ac.ActualReturnDate IS NULL THEN ac.QuantityOut ELSE 0 END), 0) > 0
                             ORDER BY a.AssetName";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OptionItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }

    public async Task CreateCheckoutAsync(CheckoutCreateViewModel checkout)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.AssetCheckouts
                             (AssetId, PersonId, QuantityOut, ExpectedReturnDate, ConditionOut, CheckoutNotes, Status)
                             VALUES
                             (@AssetId, @PersonId, @QuantityOut, @ExpectedReturnDate, @ConditionOut, @CheckoutNotes, 'Checked Out')";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetId", checkout.AssetId);
        cmd.Parameters.AddWithValue("@PersonId", checkout.PersonId);
        cmd.Parameters.AddWithValue("@QuantityOut", checkout.QuantityOut < 1 ? 1 : checkout.QuantityOut);
        cmd.Parameters.AddWithValue("@ExpectedReturnDate", checkout.ExpectedReturnDate.HasValue ? checkout.ExpectedReturnDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ConditionOut", string.IsNullOrWhiteSpace(checkout.ConditionOut) ? DBNull.Value : checkout.ConditionOut.Trim());
        cmd.Parameters.AddWithValue("@CheckoutNotes", string.IsNullOrWhiteSpace(checkout.CheckoutNotes) ? DBNull.Value : checkout.CheckoutNotes.Trim());
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }


    public async Task<int> GetAvailableAssetQuantityAsync(int assetId)
    {
        const string sql = @"SELECT a.TotalQuantity - COALESCE(SUM(CASE WHEN ac.ActualReturnDate IS NULL THEN ac.QuantityOut ELSE 0 END), 0)
                             FROM dbo.Assets a
                             LEFT JOIN dbo.AssetCheckouts ac ON a.AssetId = ac.AssetId
                             WHERE a.AssetId = @AssetId AND a.IsActive = 1
                             GROUP BY a.TotalQuantity";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AssetId", assetId);
        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    public async Task<CheckoutReturnViewModel?> GetCheckoutForReturnAsync(int id)
    {
        const string sql = @"SELECT ac.CheckoutId, a.AssetName, p.FirstName + ' ' + p.LastName AS Borrower,
                                    ac.QuantityOut, ac.CheckoutDate, ac.ExpectedReturnDate
                             FROM dbo.AssetCheckouts ac
                             JOIN dbo.Assets a ON ac.AssetId = a.AssetId
                             JOIN dbo.People p ON ac.PersonId = p.PersonId
                             WHERE ac.CheckoutId = @CheckoutId
                               AND ac.ActualReturnDate IS NULL";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CheckoutId", id);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new CheckoutReturnViewModel
        {
            CheckoutId = r.GetInt32(0),
            AssetName = r.GetString(1),
            Borrower = r.GetString(2),
            QuantityOut = r.GetInt32(3),
            CheckoutDate = r.GetDateTime(4),
            ExpectedReturnDate = r.IsDBNull(5) ? null : r.GetDateTime(5)
        };
    }

    public async Task ReturnCheckoutAsync(CheckoutReturnViewModel model)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.AssetCheckouts
                             SET ActualReturnDate = SYSDATETIME(),
                                 ConditionReturned = @ConditionReturned,
                                 ReturnNotes = @ReturnNotes,
                                 Status = 'Returned'
                             WHERE CheckoutId = @CheckoutId
                               AND ActualReturnDate IS NULL";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CheckoutId", model.CheckoutId);
        cmd.Parameters.AddWithValue("@ConditionReturned", string.IsNullOrWhiteSpace(model.ConditionReturned) ? DBNull.Value : model.ConditionReturned.Trim());
        cmd.Parameters.AddWithValue("@ReturnNotes", string.IsNullOrWhiteSpace(model.ReturnNotes) ? DBNull.Value : model.ReturnNotes.Trim());
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<KeyAssignmentRow>> GetKeyAssignmentsAsync()
    {
        var list = new List<KeyAssignmentRow>();
        const string sql = @"SELECT ka.KeyAssignmentId,
                                    k.KeyName,
                                    k.KeyCode,
                                    p.FirstName + ' ' + p.LastName AS KeyHolder,
                                    ka.IssuedDate,
                                    ka.ReturnedDate,
                                    ka.Status
                             FROM dbo.KeyAssignments ka
                             JOIN dbo.Keys k ON ka.KeyId = k.KeyId
                             JOIN dbo.People p ON ka.PersonId = p.PersonId
                             ORDER BY CASE WHEN ka.ReturnedDate IS NULL THEN 0 ELSE 1 END, ka.IssuedDate DESC";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new KeyAssignmentRow { KeyAssignmentId = r.GetInt32(0), KeyName = r.GetString(1), KeyCode = r.GetString(2), KeyHolder = r.GetString(3), IssuedDate = r.GetDateTime(4), ReturnedDate = r.IsDBNull(5) ? null : r.GetDateTime(5), Status = r.GetString(6) });
        return list;
    }


    public async Task<List<OptionItem>> GetAvailableKeyOptionsAsync()
    {
        var list = new List<OptionItem>();
        const string sql = @"SELECT k.KeyId,
                                    k.KeyName + COALESCE(' - ' + NULLIF(k.KeyCode, ''), '') AS DisplayName
                             FROM dbo.Keys k
                             WHERE k.IsActive = 1
                               AND NOT EXISTS (
                                   SELECT 1
                                   FROM dbo.KeyAssignments ka
                                   WHERE ka.KeyId = k.KeyId
                                     AND ka.ReturnedDate IS NULL
                               )
                             ORDER BY k.KeyName, k.KeyCode";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OptionItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }

    public async Task<bool> IsKeyAvailableAsync(int keyId)
    {
        const string sql = @"SELECT COUNT(*)
                             FROM dbo.Keys k
                             WHERE k.KeyId = @KeyId
                               AND k.IsActive = 1
                               AND NOT EXISTS (
                                   SELECT 1
                                   FROM dbo.KeyAssignments ka
                                   WHERE ka.KeyId = k.KeyId
                                     AND ka.ReturnedDate IS NULL
                               )";
        return await CountScalarWithIdAsync(sql, "@KeyId", keyId) > 0;
    }

    private async Task<int> CountScalarWithIdAsync(string sql, string parameterName, int id)
    {
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(parameterName, id);
        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task CreateKeyAssignmentAsync(KeyAssignmentCreateViewModel model)
    {
        await using var conn = CreateConnection();
        const string sql = @"INSERT INTO dbo.KeyAssignments
                             (KeyId, PersonId, ReasonIssued, Notes, Status)
                             VALUES
                             (@KeyId, @PersonId, @ReasonIssued, @Notes, 'Issued')";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyId", model.KeyId);
        cmd.Parameters.AddWithValue("@PersonId", model.PersonId);
        cmd.Parameters.AddWithValue("@ReasonIssued", string.IsNullOrWhiteSpace(model.ReasonIssued) ? DBNull.Value : model.ReasonIssued.Trim());
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim());
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<KeyAssignmentReturnViewModel?> GetKeyAssignmentForReturnAsync(int id)
    {
        const string sql = @"SELECT ka.KeyAssignmentId,
                                    k.KeyName,
                                    k.KeyCode,
                                    p.FirstName + ' ' + p.LastName AS KeyHolder,
                                    ka.IssuedDate,
                                    ka.ReasonIssued,
                                    ka.Notes
                             FROM dbo.KeyAssignments ka
                             JOIN dbo.Keys k ON ka.KeyId = k.KeyId
                             JOIN dbo.People p ON ka.PersonId = p.PersonId
                             WHERE ka.KeyAssignmentId = @KeyAssignmentId
                               AND ka.ReturnedDate IS NULL";
        await using var conn = CreateConnection();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyAssignmentId", id);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new KeyAssignmentReturnViewModel
        {
            KeyAssignmentId = r.GetInt32(0),
            KeyName = r.GetString(1),
            KeyCode = r.GetString(2),
            KeyHolder = r.GetString(3),
            IssuedDate = r.GetDateTime(4),
            ReasonIssued = r.IsDBNull(5) ? null : r.GetString(5),
            ExistingNotes = r.IsDBNull(6) ? null : r.GetString(6)
        };
    }

    public async Task ReturnKeyAssignmentAsync(KeyAssignmentReturnViewModel model)
    {
        await using var conn = CreateConnection();
        const string sql = @"UPDATE dbo.KeyAssignments
                             SET ReturnedDate = SYSDATETIME(),
                                 Status = 'Returned',
                                 Notes = CASE
                                     WHEN @ReturnNotes IS NULL THEN Notes
                                     WHEN Notes IS NULL OR LTRIM(RTRIM(Notes)) = '' THEN @ReturnNotes
                                     ELSE Notes + CHAR(13) + CHAR(10) + 'Return Notes: ' + @ReturnNotes
                                 END
                             WHERE KeyAssignmentId = @KeyAssignmentId
                               AND ReturnedDate IS NULL";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@KeyAssignmentId", model.KeyAssignmentId);
        cmd.Parameters.AddWithValue("@ReturnNotes", string.IsNullOrWhiteSpace(model.ReturnNotes) ? DBNull.Value : model.ReturnNotes.Trim());
        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}
