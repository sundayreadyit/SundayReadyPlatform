using ChurchAssetTracker.Models;
using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<ITDocumentRow>> GetITDocumentsAsync(string search = "", string category = "All", bool includeInactive = false)
    {
        var list = new List<ITDocumentRow>();

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT ITDocumentId, Category, Title, Description, OriginalFileName, FilePath,
                   ContentType, FileSizeBytes, UploadedBy, UploadedDate, IsActive
            FROM dbo.ITDocuments
            WHERE (@IncludeInactive = 1 OR IsActive = 1)
              AND (@Category = 'All' OR Category = @Category)
              AND (
                    @Search = ''
                    OR Title LIKE '%' + @Search + '%'
                    OR Description LIKE '%' + @Search + '%'
                    OR OriginalFileName LIKE '%' + @Search + '%'
                    OR Category LIKE '%' + @Search + '%'
                  )
            ORDER BY UploadedDate DESC, ITDocumentId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", search ?? "");
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(category) ? "All" : category.Trim());
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
            list.Add(ReadITDocument(r));

        return list;
    }

    public async Task<ITDocumentRow?> GetITDocumentAsync(int id)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT ITDocumentId, Category, Title, Description, OriginalFileName, FilePath,
                   ContentType, FileSizeBytes, UploadedBy, UploadedDate, IsActive
            FROM dbo.ITDocuments
            WHERE ITDocumentId = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        return ReadITDocument(r);
    }

    public async Task<int> CreateITDocumentAsync(ITDocumentRow document)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.ITDocuments
            (Category, Title, Description, OriginalFileName, FilePath, ContentType, FileSizeBytes, UploadedBy)
            VALUES
            (@Category, @Title, @Description, @OriginalFileName, @FilePath, @ContentType, @FileSizeBytes, @UploadedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(document.Category) ? "Other" : document.Category.Trim());
        cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(document.Title) ? document.OriginalFileName : document.Title.Trim());
        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(document.Description) ? DBNull.Value : document.Description.Trim());
        cmd.Parameters.AddWithValue("@OriginalFileName", document.OriginalFileName.Trim());
        cmd.Parameters.AddWithValue("@FilePath", document.FilePath.Trim());
        cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrWhiteSpace(document.ContentType) ? DBNull.Value : document.ContentType.Trim());
        cmd.Parameters.AddWithValue("@FileSizeBytes", document.FileSizeBytes);
        cmd.Parameters.AddWithValue("@UploadedBy", string.IsNullOrWhiteSpace(document.UploadedBy) ? DBNull.Value : document.UploadedBy.Trim());

        await conn.OpenAsync();

        return (int)await cmd.ExecuteScalarAsync();
    }

    public async Task DeactivateITDocumentAsync(int id)
    {
        await using var conn = CreateConnection();

        const string sql = "UPDATE dbo.ITDocuments SET IsActive = 0 WHERE ITDocumentId = @Id;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static ITDocumentRow ReadITDocument(SqlDataReader r) => new ITDocumentRow
    {
        ITDocumentId = r.GetInt32(0),
        Category = r.GetString(1),
        Title = r.GetString(2),
        Description = r.IsDBNull(3) ? null : r.GetString(3),
        OriginalFileName = r.GetString(4),
        FilePath = r.GetString(5),
        ContentType = r.IsDBNull(6) ? null : r.GetString(6),
        FileSizeBytes = r.GetInt64(7),
        UploadedBy = r.IsDBNull(8) ? null : r.GetString(8),
        UploadedDate = r.GetDateTime(9),
        IsActive = r.GetBoolean(10)
    };
}
