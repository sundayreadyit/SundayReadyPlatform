using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<GlobalSearchResultItem>> SearchPeopleAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                PersonId,
                FirstName + ' ' + LastName AS FullName,
                Email,
                Phone
            FROM dbo.People
            WHERE IsActive = 1
              AND
              (
                  FirstName LIKE @Search
                  OR LastName LIKE @Search
                  OR Email LIKE @Search
                  OR Phone LIKE @Search
              )
            ORDER BY LastName, FirstName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var email = r.IsDBNull(2) ? "" : r.GetString(2);
            var phone = r.IsDBNull(3) ? "" : r.GetString(3);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = email,
                Detail = phone,
                Url = $"/People/Details/{id}",
                Badge = "Person"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchStudentsAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                StudentId,
                FirstName + ' ' + LastName AS FullName,
                GradeLevel,
                Classroom
            FROM dbo.Students
            WHERE IsActive = 1
              AND
              (
                  FirstName LIKE @Search
                  OR LastName LIKE @Search
                  OR GradeLevel LIKE @Search
                  OR Classroom LIKE @Search
              )
            ORDER BY LastName, FirstName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var grade = r.IsDBNull(2) ? "" : r.GetString(2);
            var room = r.IsDBNull(3) ? "" : r.GetString(3);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = $"Grade: {grade}",
                Detail = string.IsNullOrWhiteSpace(room) ? null : $"Classroom: {room}",
                Url = $"/Students/Details/{id}",
                Badge = "Student"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchFacultyStaffAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                FacultyStaffId,
                FirstName + ' ' + LastName AS FullName,
                Email
            FROM dbo.FacultyStaff
            WHERE IsActive = 1
              AND
              (
                  FirstName LIKE @Search
                  OR LastName LIKE @Search
                  OR Email LIKE @Search
              )
            ORDER BY LastName, FirstName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var email = r.IsDBNull(2) ? "" : r.GetString(2);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = email,
                Detail = "Faculty / Staff",
                Url = $"/FacultyStaff/Details/{id}",
                Badge = "Faculty"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchITSupportTicketsAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                TicketId,
                TicketNumber,
                Title,
                Priority,
                Status,
                RequestedByName
            FROM dbo.ITSupportTickets
            WHERE
                TicketNumber LIKE @Search
                OR Title LIKE @Search
                OR Description LIKE @Search
                OR RequestedByName LIKE @Search
                OR RequestedByEmail LIKE @Search
                OR Category LIKE @Search
            ORDER BY CreatedDate DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var ticketNumber = r.IsDBNull(1) ? $"IT-{id:00000}" : r.GetString(1);
            var title = r.GetString(2);
            var priority = r.GetString(3);
            var status = r.GetString(4);
            var requester = r.IsDBNull(5) ? "" : r.GetString(5);

            results.Add(new GlobalSearchResultItem
            {
                Title = $"{ticketNumber} - {title}",
                Subtitle = $"{priority} • {status}",
                Detail = requester,
                Url = $"/ITSupport/Details/{id}",
                Badge = "IT Ticket"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchITAssetsAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                ITAssetId,
                AssetName,
                AssetTag,
                Make,
                Model,
                IPAddress
            FROM dbo.ITAssets
            WHERE IsActive = 1
              AND
              (
                  AssetName LIKE @Search
                  OR AssetTag LIKE @Search
                  OR Make LIKE @Search
                  OR Model LIKE @Search
                  OR SerialNumber LIKE @Search
                  OR IPAddress LIKE @Search
              )
            ORDER BY AssetName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var tag = r.IsDBNull(2) ? "" : r.GetString(2);
            var make = r.IsDBNull(3) ? "" : r.GetString(3);
            var model = r.IsDBNull(4) ? "" : r.GetString(4);
            var ip = r.IsDBNull(5) ? "" : r.GetString(5);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = $"{make} {model}".Trim(),
                Detail = string.Join(" • ", new[] { tag, ip }.Where(x => !string.IsNullOrWhiteSpace(x))),
                Url = $"/ITAssets/Details/{id}",
                Badge = "IT Asset"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchAssetsAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                AssetId,
                AssetName,
                AssetTag,
                Category
            FROM dbo.Assets
            WHERE IsActive = 1
              AND
              (
                  AssetName LIKE @Search
                  OR AssetTag LIKE @Search
                  OR Category LIKE @Search
              )
            ORDER BY AssetName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var tag = r.IsDBNull(2) ? "" : r.GetString(2);
            var category = r.IsDBNull(3) ? "" : r.GetString(3);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = category,
                Detail = tag,
                Url = $"/Assets/Details/{id}",
                Badge = "Asset"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchReservationsAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                ReservationId,
                EventName,
                Status,
                StartDateTime
            FROM dbo.Reservations
            WHERE
                EventName LIKE @Search
                OR Status LIKE @Search
            ORDER BY StartDateTime DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var eventName = r.GetString(1);
            var status = r.IsDBNull(2) ? "" : r.GetString(2);
            var start = r.GetDateTime(3);

            results.Add(new GlobalSearchResultItem
            {
                Title = eventName,
                Subtitle = $"{status} • {start:g}",
                Detail = "Reservation",
                Url = $"/Reservations/Details/{id}",
                Badge = "Reservation"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchKeysAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                KeyId,
                KeyName,
                KeyCode
            FROM dbo.Keys
            WHERE IsActive = 1
              AND
              (
                  KeyName LIKE @Search
                  OR KeyCode LIKE @Search
              )
            ORDER BY KeyName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var code = r.IsDBNull(2) ? "" : r.GetString(2);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = code,
                Detail = "Key",
                Url = $"/Keys/Details/{id}",
                Badge = "Key"
            });
        }

        return results;
    }

    public async Task<List<GlobalSearchResultItem>> SearchAccessAreasAsync(string query, int take = 10)
    {
        var results = new List<GlobalSearchResultItem>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        await using var conn = CreateConnection();

        const string sql = @"
            SELECT TOP (@Take)
                AccessAreaId,
                AreaName,
                Description
            FROM dbo.AccessAreas
            WHERE IsActive = 1
              AND
              (
                  AreaName LIKE @Search
                  OR Description LIKE @Search
              )
            ORDER BY AreaName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Take", take);
        cmd.Parameters.AddWithValue("@Search", $"%{query.Trim()}%");

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            var id = r.GetInt32(0);
            var name = r.GetString(1);
            var description = r.IsDBNull(2) ? "" : r.GetString(2);

            results.Add(new GlobalSearchResultItem
            {
                Title = name,
                Subtitle = "Access Area",
                Detail = description,
                Url = $"/AccessAreas/Details/{id}",
                Badge = "Area"
            });
        }

        return results;
    }
}