using ChurchAssetTracker.Models;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Services;

public class WorshipPlanningService
{
    private readonly string _connectionString;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    public WorshipPlanningService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection in appsettings.json");
    }

    public async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = @"
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
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'WorshipLeader')
BEGIN
    INSERT INTO dbo.Roles(RoleName, Description) VALUES('WorshipLeader', 'Access to Worship Planning, song library, service sets, and worship packets');
END;";
            await using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            _schemaReady = true;
        }
        finally { SchemaLock.Release(); }
    }

    public async Task<int> CreateSetAsync(WorshipSetInputModel model, string createdBy)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"INSERT INTO dbo.WorshipSets(ServiceDate,Title,Notes,Status,CreatedBy)
VALUES(@Date,@Title,@Notes,@Status,@By); SELECT CAST(SCOPE_IDENTITY() AS int);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Date", model.ServiceDate.Date);
        cmd.Parameters.AddWithValue("@Title", model.Title.Trim());
        cmd.Parameters.AddWithValue("@Notes", (object?)model.Notes?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", model.Status);
        cmd.Parameters.AddWithValue("@By", createdBy ?? "");
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    public async Task UpdateSetAsync(WorshipSetInputModel model)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"UPDATE dbo.WorshipSets SET ServiceDate=@Date,Title=@Title,Notes=@Notes,Status=@Status,UpdatedDate=SYSDATETIME() WHERE Id=@Id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", model.Id);
        cmd.Parameters.AddWithValue("@Date", model.ServiceDate.Date);
        cmd.Parameters.AddWithValue("@Title", model.Title.Trim());
        cmd.Parameters.AddWithValue("@Notes", (object?)model.Notes?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", model.Status);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<WorshipSetDetail?> GetSetAsync(int id)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        WorshipSetDetail? set = null;
        await using (var cmd = new SqlCommand("SELECT Id,ServiceDate,Title,Notes,Status,CreatedBy,CreatedDate,UpdatedDate FROM dbo.WorshipSets WHERE Id=@Id", conn))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                set = new WorshipSetDetail
                {
                    Id = r.GetInt32(0), ServiceDate = r.GetDateTime(1), Title = r.GetString(2),
                    Notes = r.IsDBNull(3) ? "" : r.GetString(3), Status = r.GetString(4),
                    CreatedBy = r.IsDBNull(5) ? "" : r.GetString(5), CreatedDate = r.GetDateTime(6), UpdatedDate = r.GetDateTime(7)
                };
            }
        }
        if (set == null) return null;
        await using (var cmd = new SqlCommand("SELECT Id,WorshipSetId,SortOrder,SongTitle,RelativePath,KeyOverride,Leader,Notes FROM dbo.WorshipSetItems WHERE WorshipSetId=@Id ORDER BY SortOrder,Id", conn))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                set.Items.Add(new WorshipSetItemModel
                {
                    Id=r.GetInt32(0), WorshipSetId=r.GetInt32(1), SortOrder=r.GetInt32(2), SongTitle=r.GetString(3), RelativePath=r.GetString(4),
                    KeyOverride=r.IsDBNull(5)?"":r.GetString(5), Leader=r.IsDBNull(6)?"":r.GetString(6), Notes=r.IsDBNull(7)?"":r.GetString(7)
                });
        }
        return set;
    }

    public async Task<List<WorshipSetSummary>> GetUpcomingAsync(int take = 8)
        => await GetSummariesAsync("s.ServiceDate >= CAST(GETDATE() AS date)", "s.ServiceDate ASC", take);

    public async Task<List<WorshipSetSummary>> GetRecentAsync(int take = 12)
        => await GetSummariesAsync("s.ServiceDate < CAST(GETDATE() AS date)", "s.ServiceDate DESC", take);

    private async Task<List<WorshipSetSummary>> GetSummariesAsync(string where, string order, int take)
    {
        await EnsureSchemaAsync();
        var list = new List<WorshipSetSummary>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = $@"SELECT TOP (@Take) s.Id,s.ServiceDate,s.Title,s.Status,s.UpdatedDate,COUNT(i.Id) SongCount
FROM dbo.WorshipSets s LEFT JOIN dbo.WorshipSetItems i ON i.WorshipSetId=s.Id
WHERE {where} GROUP BY s.Id,s.ServiceDate,s.Title,s.Status,s.UpdatedDate ORDER BY {order}";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        await using var r = await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync()) list.Add(new WorshipSetSummary { Id=r.GetInt32(0), ServiceDate=r.GetDateTime(1), Title=r.GetString(2), Status=r.GetString(3), UpdatedDate=r.GetDateTime(4), SongCount=r.GetInt32(5)});
        return list;
    }

    public async Task AddSongAsync(int setId, string title, string relativePath)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql=@"DECLARE @Sort INT=(SELECT ISNULL(MAX(SortOrder),0)+1 FROM dbo.WorshipSetItems WHERE WorshipSetId=@SetId);
INSERT INTO dbo.WorshipSetItems(WorshipSetId,SortOrder,SongTitle,RelativePath) VALUES(@SetId,@Sort,@Title,@Path);
UPDATE dbo.WorshipSets SET UpdatedDate=SYSDATETIME() WHERE Id=@SetId;";
        await using var cmd=new SqlCommand(sql,conn);
        cmd.Parameters.AddWithValue("@SetId",setId); cmd.Parameters.AddWithValue("@Title",title); cmd.Parameters.AddWithValue("@Path",relativePath);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveSongAsync(int itemId)
    {
        await EnsureSchemaAsync();
        await using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
        const string sql=@"DECLARE @SetId INT=(SELECT WorshipSetId FROM dbo.WorshipSetItems WHERE Id=@Id); DELETE FROM dbo.WorshipSetItems WHERE Id=@Id;
;WITH x AS (SELECT Id,ROW_NUMBER() OVER(ORDER BY SortOrder,Id) rn FROM dbo.WorshipSetItems WHERE WorshipSetId=@SetId) UPDATE i SET SortOrder=x.rn FROM dbo.WorshipSetItems i JOIN x ON x.Id=i.Id;
UPDATE dbo.WorshipSets SET UpdatedDate=SYSDATETIME() WHERE Id=@SetId;";
        await using var cmd=new SqlCommand(sql,conn); cmd.Parameters.AddWithValue("@Id",itemId); await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateItemAsync(WorshipSetItemModel item)
    {
        await EnsureSchemaAsync();
        await using var conn=new SqlConnection(_connectionString); await conn.OpenAsync();
        const string sql=@"UPDATE dbo.WorshipSetItems SET KeyOverride=@Key,Leader=@Leader,Notes=@Notes WHERE Id=@Id; UPDATE dbo.WorshipSets SET UpdatedDate=SYSDATETIME() WHERE Id=(SELECT WorshipSetId FROM dbo.WorshipSetItems WHERE Id=@Id);";
        await using var cmd=new SqlCommand(sql,conn); cmd.Parameters.AddWithValue("@Id",item.Id); cmd.Parameters.AddWithValue("@Key",(object?)item.KeyOverride??DBNull.Value); cmd.Parameters.AddWithValue("@Leader",(object?)item.Leader??DBNull.Value); cmd.Parameters.AddWithValue("@Notes",(object?)item.Notes??DBNull.Value); await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReorderAsync(int setId, IEnumerable<int> itemIds)
    {
        await EnsureSchemaAsync();
        await using var conn=new SqlConnection(_connectionString); await conn.OpenAsync();
        var order=1;
        foreach(var id in itemIds)
        {
            await using var cmd=new SqlCommand("UPDATE dbo.WorshipSetItems SET SortOrder=@Order WHERE Id=@Id AND WorshipSetId=@SetId",conn);
            cmd.Parameters.AddWithValue("@Order",order++); cmd.Parameters.AddWithValue("@Id",id); cmd.Parameters.AddWithValue("@SetId",setId); await cmd.ExecuteNonQueryAsync();
        }
        await using var touch=new SqlCommand("UPDATE dbo.WorshipSets SET UpdatedDate=SYSDATETIME() WHERE Id=@Id",conn); touch.Parameters.AddWithValue("@Id",setId); await touch.ExecuteNonQueryAsync();
    }

    public async Task<int> DuplicateAsync(int id, string createdBy)
    {
        await EnsureSchemaAsync();
        await using var conn=new SqlConnection(_connectionString); await conn.OpenAsync();
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            const string s=@"INSERT INTO dbo.WorshipSets(ServiceDate,Title,Notes,Status,CreatedBy)
SELECT DATEADD(day,7,ServiceDate),Title + ' (Copy)',Notes,'Draft',@By FROM dbo.WorshipSets WHERE Id=@Id; SELECT CAST(SCOPE_IDENTITY() AS int);";
            await using var cmd=new SqlCommand(s,conn,tx); cmd.Parameters.AddWithValue("@Id",id); cmd.Parameters.AddWithValue("@By",createdBy??"");
            var newId=(int)(await cmd.ExecuteScalarAsync()??0);
            await using var items=new SqlCommand(@"INSERT INTO dbo.WorshipSetItems(WorshipSetId,SortOrder,SongTitle,RelativePath,KeyOverride,Leader,Notes)
SELECT @NewId,SortOrder,SongTitle,RelativePath,KeyOverride,Leader,Notes FROM dbo.WorshipSetItems WHERE WorshipSetId=@Id",conn,tx);
            items.Parameters.AddWithValue("@NewId",newId); items.Parameters.AddWithValue("@Id",id); await items.ExecuteNonQueryAsync();
            await tx.CommitAsync(); return newId;
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task DeleteSetAsync(int id)
    {
        await EnsureSchemaAsync();
        await using var conn=new SqlConnection(_connectionString); await conn.OpenAsync(); await using var cmd=new SqlCommand("DELETE FROM dbo.WorshipSets WHERE Id=@Id",conn); cmd.Parameters.AddWithValue("@Id",id); await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string,WorshipSongUsage>> GetUsageAsync()
    {
        await EnsureSchemaAsync();
        var dict=new Dictionary<string,WorshipSongUsage>(StringComparer.OrdinalIgnoreCase);
        await using var conn=new SqlConnection(_connectionString); await conn.OpenAsync();
        const string sql=@"SELECT i.RelativePath,COUNT(DISTINCT s.Id),MAX(s.ServiceDate) FROM dbo.WorshipSetItems i JOIN dbo.WorshipSets s ON s.Id=i.WorshipSetId WHERE s.ServiceDate <= CAST(GETDATE() AS date) GROUP BY i.RelativePath";
        await using var cmd=new SqlCommand(sql,conn); await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync()) dict[r.GetString(0)]=new WorshipSongUsage{RelativePath=r.GetString(0),TimesUsed=r.GetInt32(1),LastUsedDate=r.IsDBNull(2)?null:r.GetDateTime(2)};
        return dict;
    }
}
