using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<ITSupportTicketAttachmentRow>> GetITSupportTicketAttachmentsAsync(int ticketId)
    {
        var list = new List<ITSupportTicketAttachmentRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT a.AttachmentId, a.TicketId, a.OriginalFileName, a.StoredFileName, a.FilePath,
                   a.ContentType, a.FileSizeBytes, a.UploadedByUserId, u.DisplayName, a.UploadedDate
            FROM dbo.ITSupportTicketAttachments a
            LEFT JOIN dbo.Users u ON a.UploadedByUserId = u.UserId
            WHERE a.TicketId = @TicketId
            ORDER BY a.UploadedDate DESC, a.AttachmentId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ITSupportTicketAttachmentRow
            {
                AttachmentId = r.GetInt32(0),
                TicketId = r.GetInt32(1),
                OriginalFileName = r.GetString(2),
                StoredFileName = r.GetString(3),
                FilePath = r.GetString(4),
                ContentType = r.IsDBNull(5) ? null : r.GetString(5),
                FileSizeBytes = r.GetInt64(6),
                UploadedByUserId = r.IsDBNull(7) ? null : r.GetInt32(7),
                UploadedByName = r.IsDBNull(8) ? null : r.GetString(8),
                UploadedDate = r.GetDateTime(9)
            });
        }

        return list;
    }

    public async Task<int> CreateITSupportTicketAttachmentAsync(int ticketId, string originalFileName, string storedFileName, string filePath, string? contentType, long fileSizeBytes, int? uploadedByUserId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.ITSupportTicketAttachments
            (TicketId, OriginalFileName, StoredFileName, FilePath, ContentType, FileSizeBytes, UploadedByUserId)
            OUTPUT INSERTED.AttachmentId
            VALUES
            (@TicketId, @OriginalFileName, @StoredFileName, @FilePath, @ContentType, @FileSizeBytes, @UploadedByUserId);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        cmd.Parameters.AddWithValue("@OriginalFileName", originalFileName);
        cmd.Parameters.AddWithValue("@StoredFileName", storedFileName);
        cmd.Parameters.AddWithValue("@FilePath", filePath);
        cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrWhiteSpace(contentType) ? DBNull.Value : contentType);
        cmd.Parameters.AddWithValue("@FileSizeBytes", fileSizeBytes);
        cmd.Parameters.AddWithValue("@UploadedByUserId", uploadedByUserId.HasValue ? uploadedByUserId.Value : DBNull.Value);

        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        var attachmentId = Convert.ToInt32(result);

        await LogAuditAsync("UploadAttachment", "ITSupportTicket", ticketId, $"Uploaded attachment '{originalFileName}' to IT support ticket.", uploadedByUserId);

        return attachmentId;
    }

    public async Task<ITSupportTicketAttachmentRow?> GetITSupportTicketAttachmentAsync(int attachmentId)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT a.AttachmentId, a.TicketId, a.OriginalFileName, a.StoredFileName, a.FilePath,
                   a.ContentType, a.FileSizeBytes, a.UploadedByUserId, u.DisplayName, a.UploadedDate
            FROM dbo.ITSupportTicketAttachments a
            LEFT JOIN dbo.Users u ON a.UploadedByUserId = u.UserId
            WHERE a.AttachmentId = @AttachmentId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
        await conn.OpenAsync();

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new ITSupportTicketAttachmentRow
        {
            AttachmentId = r.GetInt32(0),
            TicketId = r.GetInt32(1),
            OriginalFileName = r.GetString(2),
            StoredFileName = r.GetString(3),
            FilePath = r.GetString(4),
            ContentType = r.IsDBNull(5) ? null : r.GetString(5),
            FileSizeBytes = r.GetInt64(6),
            UploadedByUserId = r.IsDBNull(7) ? null : r.GetInt32(7),
            UploadedByName = r.IsDBNull(8) ? null : r.GetString(8),
            UploadedDate = r.GetDateTime(9)
        };
    }
}
