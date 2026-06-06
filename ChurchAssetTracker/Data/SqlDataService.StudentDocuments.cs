using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<StudentDocumentRow>> GetStudentDocumentsAsync(int studentId, bool includeInactive = false)
    {
        var list = new List<StudentDocumentRow>();

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT StudentDocumentId, StudentId, DocumentType, DocumentTitle, OriginalFileName, FilePath,
                   ContentType, FileSizeBytes, Notes, UploadedBy, UploadedDate, IsActive
            FROM dbo.StudentDocuments
            WHERE StudentId = @StudentId
              AND (@IncludeInactive = 1 OR IsActive = 1)
            ORDER BY UploadedDate DESC, StudentDocumentId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@StudentId", studentId);
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
            list.Add(ReadStudentDocument(r));

        return list;
    }

    public async Task<StudentDocumentRow?> GetStudentDocumentAsync(int studentDocumentId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT StudentDocumentId, StudentId, DocumentType, DocumentTitle, OriginalFileName, FilePath,
                   ContentType, FileSizeBytes, Notes, UploadedBy, UploadedDate, IsActive
            FROM dbo.StudentDocuments
            WHERE StudentDocumentId = @StudentDocumentId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@StudentDocumentId", studentDocumentId);

        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        return ReadStudentDocument(r);
    }

    public async Task<int> CreateStudentDocumentAsync(StudentDocumentRow document)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.StudentDocuments
            (StudentId, DocumentType, DocumentTitle, OriginalFileName, FilePath, ContentType, FileSizeBytes, Notes, UploadedBy)
            VALUES
            (@StudentId, @DocumentType, @DocumentTitle, @OriginalFileName, @FilePath, @ContentType, @FileSizeBytes, @Notes, @UploadedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@StudentId", document.StudentId);
        cmd.Parameters.AddWithValue("@DocumentType", string.IsNullOrWhiteSpace(document.DocumentType) ? "Other" : document.DocumentType.Trim());
        cmd.Parameters.AddWithValue("@DocumentTitle", string.IsNullOrWhiteSpace(document.DocumentTitle) ? document.OriginalFileName : document.DocumentTitle.Trim());
        cmd.Parameters.AddWithValue("@OriginalFileName", document.OriginalFileName.Trim());
        cmd.Parameters.AddWithValue("@FilePath", document.FilePath.Trim());
        cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrWhiteSpace(document.ContentType) ? DBNull.Value : document.ContentType.Trim());
        cmd.Parameters.AddWithValue("@FileSizeBytes", document.FileSizeBytes);
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(document.Notes) ? DBNull.Value : document.Notes.Trim());
        cmd.Parameters.AddWithValue("@UploadedBy", string.IsNullOrWhiteSpace(document.UploadedBy) ? DBNull.Value : document.UploadedBy.Trim());

        await conn.OpenAsync();

        return (int)await cmd.ExecuteScalarAsync();
    }

    public async Task DeactivateStudentDocumentAsync(int studentDocumentId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.StudentDocuments
            SET IsActive = 0
            WHERE StudentDocumentId = @StudentDocumentId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@StudentDocumentId", studentDocumentId);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    private static StudentDocumentRow ReadStudentDocument(SqlDataReader r) => new StudentDocumentRow
    {
        StudentDocumentId = r.GetInt32(0),
        StudentId = r.GetInt32(1),
        DocumentType = r.GetString(2),
        DocumentTitle = r.GetString(3),
        OriginalFileName = r.GetString(4),
        FilePath = r.GetString(5),
        ContentType = r.IsDBNull(6) ? null : r.GetString(6),
        FileSizeBytes = r.GetInt64(7),
        Notes = r.IsDBNull(8) ? null : r.GetString(8),
        UploadedBy = r.IsDBNull(9) ? null : r.GetString(9),
        UploadedDate = r.GetDateTime(10),
        IsActive = r.GetBoolean(11)
    };
}
