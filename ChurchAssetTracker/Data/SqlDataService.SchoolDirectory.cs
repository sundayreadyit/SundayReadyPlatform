using Microsoft.Data.SqlClient;

namespace ChurchAssetTracker.Data;

public partial class SqlDataService
{
    public async Task<List<StudentRow>> GetStudentsAsync(string search = "", bool includeInactive = false)
    {
        var list = new List<StudentRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT s.StudentId, s.FirstName, s.LastName, s.PreferredName, s.DateOfBirth, s.GradeLevel, s.Classroom,
                   s.TeacherFacultyStaffId,
                   CASE
                       WHEN fs.FacultyStaffId IS NULL THEN NULL
                       WHEN fs.PreferredName IS NOT NULL AND LTRIM(RTRIM(fs.PreferredName)) <> ''
                            THEN fs.PreferredName + ' ' + fs.LastName
                       ELSE fs.FirstName + ' ' + fs.LastName
                   END AS TeacherName,
                   s.PhotoPath,
                   s.ParentGuardian1Name, s.ParentGuardian1Phone, s.ParentGuardian1Email,
                   s.ParentGuardian2Name, s.ParentGuardian2Phone, s.ParentGuardian2Email,
                   s.EmergencyContactName, s.EmergencyContactPhone, s.EmergencyContactRelationship,
                   s.AllergiesMedicalNotes, s.AuthorizedPickupNotes, s.Notes, s.IsActive
            FROM dbo.Students s
            LEFT JOIN dbo.FacultyStaff fs ON s.TeacherFacultyStaffId = fs.FacultyStaffId
            WHERE (@IncludeInactive = 1 OR s.IsActive = 1)
              AND (
                    @Search = ''
                    OR s.FirstName LIKE '%' + @Search + '%'
                    OR s.LastName LIKE '%' + @Search + '%'
                    OR s.PreferredName LIKE '%' + @Search + '%'
                    OR s.GradeLevel LIKE '%' + @Search + '%'
                    OR s.Classroom LIKE '%' + @Search + '%'
                    OR s.ParentGuardian1Name LIKE '%' + @Search + '%'
                    OR s.ParentGuardian2Name LIKE '%' + @Search + '%'
                    OR fs.FirstName LIKE '%' + @Search + '%'
                    OR fs.LastName LIKE '%' + @Search + '%'
                  )
            ORDER BY s.LastName, s.FirstName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", search ?? "");
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync()) list.Add(ReadStudent(r));
        return list;
    }

    public async Task<StudentRow?> GetStudentAsync(int id)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT s.StudentId, s.FirstName, s.LastName, s.PreferredName, s.DateOfBirth, s.GradeLevel, s.Classroom,
                   s.TeacherFacultyStaffId,
                   CASE
                       WHEN fs.FacultyStaffId IS NULL THEN NULL
                       WHEN fs.PreferredName IS NOT NULL AND LTRIM(RTRIM(fs.PreferredName)) <> ''
                            THEN fs.PreferredName + ' ' + fs.LastName
                       ELSE fs.FirstName + ' ' + fs.LastName
                   END AS TeacherName,
                   s.PhotoPath,
                   s.ParentGuardian1Name, s.ParentGuardian1Phone, s.ParentGuardian1Email,
                   s.ParentGuardian2Name, s.ParentGuardian2Phone, s.ParentGuardian2Email,
                   s.EmergencyContactName, s.EmergencyContactPhone, s.EmergencyContactRelationship,
                   s.AllergiesMedicalNotes, s.AuthorizedPickupNotes, s.Notes, s.IsActive
            FROM dbo.Students s
            LEFT JOIN dbo.FacultyStaff fs ON s.TeacherFacultyStaffId = fs.FacultyStaffId
            WHERE s.StudentId = @Id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync()) return null;
        return ReadStudent(r);
    }

    public async Task<int> CreateStudentAsync(StudentRow model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.Students
            (FirstName, LastName, PreferredName, DateOfBirth, GradeLevel, Classroom, TeacherFacultyStaffId, PhotoPath,
             ParentGuardian1Name, ParentGuardian1Phone, ParentGuardian1Email,
             ParentGuardian2Name, ParentGuardian2Phone, ParentGuardian2Email,
             EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
             AllergiesMedicalNotes, AuthorizedPickupNotes, Notes, IsActive)
            VALUES
            (@FirstName, @LastName, @PreferredName, @DateOfBirth, @GradeLevel, @Classroom, @TeacherFacultyStaffId, @PhotoPath,
             @ParentGuardian1Name, @ParentGuardian1Phone, @ParentGuardian1Email,
             @ParentGuardian2Name, @ParentGuardian2Phone, @ParentGuardian2Email,
             @EmergencyContactName, @EmergencyContactPhone, @EmergencyContactRelationship,
             @AllergiesMedicalNotes, @AuthorizedPickupNotes, @Notes, @IsActive);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var cmd = new SqlCommand(sql, conn);
        AddStudentParameters(cmd, model);

        await conn.OpenAsync();
        var id = (int)await cmd.ExecuteScalarAsync();

        await WriteSchoolAuditLogAsync(username, "Create", "Student", id, $"Created student record: {model.FirstName} {model.LastName}");
        return id;
    }

    public async Task UpdateStudentAsync(StudentRow model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.Students
            SET FirstName=@FirstName, LastName=@LastName, PreferredName=@PreferredName, DateOfBirth=@DateOfBirth,
                GradeLevel=@GradeLevel, Classroom=@Classroom, TeacherFacultyStaffId=@TeacherFacultyStaffId, PhotoPath=@PhotoPath,
                ParentGuardian1Name=@ParentGuardian1Name, ParentGuardian1Phone=@ParentGuardian1Phone, ParentGuardian1Email=@ParentGuardian1Email,
                ParentGuardian2Name=@ParentGuardian2Name, ParentGuardian2Phone=@ParentGuardian2Phone, ParentGuardian2Email=@ParentGuardian2Email,
                EmergencyContactName=@EmergencyContactName, EmergencyContactPhone=@EmergencyContactPhone, EmergencyContactRelationship=@EmergencyContactRelationship,
                AllergiesMedicalNotes=@AllergiesMedicalNotes, AuthorizedPickupNotes=@AuthorizedPickupNotes, Notes=@Notes,
                IsActive=@IsActive, ModifiedDate=SYSDATETIME()
            WHERE StudentId=@StudentId";

        await using var cmd = new SqlCommand(sql, conn);
        AddStudentParameters(cmd, model);
        cmd.Parameters.AddWithValue("@StudentId", model.StudentId);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        await WriteSchoolAuditLogAsync(username, "Update", "Student", model.StudentId, $"Updated student record: {model.FirstName} {model.LastName}");
    }

    public async Task<List<FacultyStaffRow>> GetFacultyStaffAsync(string search = "", bool includeInactive = false)
    {
        var list = new List<FacultyStaffRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT FacultyStaffId, FirstName, LastName, PreferredName, RoleTitle, Department, Classroom,
                   Phone, Extension, Email, PhotoPath, EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
                   Notes, IsActive
            FROM dbo.FacultyStaff
            WHERE (@IncludeInactive = 1 OR IsActive = 1)
              AND (
                    @Search = ''
                    OR FirstName LIKE '%' + @Search + '%'
                    OR LastName LIKE '%' + @Search + '%'
                    OR PreferredName LIKE '%' + @Search + '%'
                    OR RoleTitle LIKE '%' + @Search + '%'
                    OR Department LIKE '%' + @Search + '%'
                    OR Classroom LIKE '%' + @Search + '%'
                    OR Extension LIKE '%' + @Search + '%'
                  )
            ORDER BY LastName, FirstName";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", search ?? "");
        cmd.Parameters.AddWithValue("@IncludeInactive", includeInactive ? 1 : 0);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync()) list.Add(ReadFacultyStaff(r));
        return list;
    }

    public async Task<FacultyStaffRow?> GetFacultyStaffMemberAsync(int id)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT FacultyStaffId, FirstName, LastName, PreferredName, RoleTitle, Department, Classroom,
                   Phone, Extension, Email, PhotoPath, EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
                   Notes, IsActive
            FROM dbo.FacultyStaff
            WHERE FacultyStaffId = @Id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync()) return null;
        return ReadFacultyStaff(r);
    }

    public async Task<int> CreateFacultyStaffAsync(FacultyStaffRow model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.FacultyStaff
            (FirstName, LastName, PreferredName, RoleTitle, Department, Classroom, Phone, Extension, Email, PhotoPath,
             EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship, Notes, IsActive)
            VALUES
            (@FirstName, @LastName, @PreferredName, @RoleTitle, @Department, @Classroom, @Phone, @Extension, @Email, @PhotoPath,
             @EmergencyContactName, @EmergencyContactPhone, @EmergencyContactRelationship, @Notes, @IsActive);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var cmd = new SqlCommand(sql, conn);
        AddFacultyStaffParameters(cmd, model);

        await conn.OpenAsync();
        var id = (int)await cmd.ExecuteScalarAsync();

        await WriteSchoolAuditLogAsync(username, "Create", "FacultyStaff", id, $"Created faculty/staff record: {model.FirstName} {model.LastName}");
        return id;
    }

    public async Task UpdateFacultyStaffAsync(FacultyStaffRow model, string username)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            UPDATE dbo.FacultyStaff
            SET FirstName=@FirstName, LastName=@LastName, PreferredName=@PreferredName,
                RoleTitle=@RoleTitle, Department=@Department, Classroom=@Classroom,
                Phone=@Phone, Extension=@Extension, Email=@Email, PhotoPath=@PhotoPath,
                EmergencyContactName=@EmergencyContactName, EmergencyContactPhone=@EmergencyContactPhone,
                EmergencyContactRelationship=@EmergencyContactRelationship, Notes=@Notes,
                IsActive=@IsActive, ModifiedDate=SYSDATETIME()
            WHERE FacultyStaffId=@FacultyStaffId";

        await using var cmd = new SqlCommand(sql, conn);
        AddFacultyStaffParameters(cmd, model);
        cmd.Parameters.AddWithValue("@FacultyStaffId", model.FacultyStaffId);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        await WriteSchoolAuditLogAsync(username, "Update", "FacultyStaff", model.FacultyStaffId, $"Updated faculty/staff record: {model.FirstName} {model.LastName}");
    }

    public async Task<List<FacultyStaffRow>> GetActiveTeacherOptionsAsync()
    {
        var list = new List<FacultyStaffRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT FacultyStaffId, FirstName, LastName, PreferredName, RoleTitle, Department, Classroom,
                   Phone, Extension, Email, PhotoPath, EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
                   Notes, IsActive
            FROM dbo.FacultyStaff
            WHERE IsActive = 1
            ORDER BY LastName, FirstName;";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync()) list.Add(ReadFacultyStaff(r));
        return list;
    }

    public async Task<List<string>> GetStudentGradeLevelsAsync()
    {
        var list = new List<string>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT DISTINCT GradeLevel
            FROM dbo.Students
            WHERE IsActive = 1
              AND GradeLevel IS NOT NULL
              AND LTRIM(RTRIM(GradeLevel)) <> ''
            ORDER BY GradeLevel;";

        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    public async Task<List<StudentRow>> GetStudentRosterAsync(int? teacherFacultyStaffId, string? gradeLevel)
    {
        var list = new List<StudentRow>();
        await using var conn = CreateConnection();

        const string sql = @"
            SELECT s.StudentId, s.FirstName, s.LastName, s.PreferredName, s.DateOfBirth, s.GradeLevel, s.Classroom,
                   s.TeacherFacultyStaffId,
                   CASE
                       WHEN fs.FacultyStaffId IS NULL THEN NULL
                       WHEN fs.PreferredName IS NOT NULL AND LTRIM(RTRIM(fs.PreferredName)) <> ''
                            THEN fs.PreferredName + ' ' + fs.LastName
                       ELSE fs.FirstName + ' ' + fs.LastName
                   END AS TeacherName,
                   s.PhotoPath,
                   s.ParentGuardian1Name, s.ParentGuardian1Phone, s.ParentGuardian1Email,
                   s.ParentGuardian2Name, s.ParentGuardian2Phone, s.ParentGuardian2Email,
                   s.EmergencyContactName, s.EmergencyContactPhone, s.EmergencyContactRelationship,
                   s.AllergiesMedicalNotes, s.AuthorizedPickupNotes, s.Notes, s.IsActive
            FROM dbo.Students s
            LEFT JOIN dbo.FacultyStaff fs ON s.TeacherFacultyStaffId = fs.FacultyStaffId
            WHERE s.IsActive = 1
              AND (@TeacherFacultyStaffId IS NULL OR s.TeacherFacultyStaffId = @TeacherFacultyStaffId)
              AND (@GradeLevel IS NULL OR @GradeLevel = '' OR s.GradeLevel = @GradeLevel)
            ORDER BY s.GradeLevel, s.LastName, s.FirstName;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TeacherFacultyStaffId", teacherFacultyStaffId.HasValue ? teacherFacultyStaffId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@GradeLevel", string.IsNullOrWhiteSpace(gradeLevel) ? DBNull.Value : gradeLevel.Trim());

        await conn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync()) list.Add(ReadStudent(r));
        return list;
    }

    private static StudentRow ReadStudent(SqlDataReader r) => new StudentRow
    {
        StudentId = r.GetInt32(0),
        FirstName = r.GetString(1),
        LastName = r.GetString(2),
        PreferredName = r.IsDBNull(3) ? null : r.GetString(3),
        DateOfBirth = r.IsDBNull(4) ? null : r.GetDateTime(4),
        GradeLevel = r.IsDBNull(5) ? null : r.GetString(5),
        Classroom = r.IsDBNull(6) ? null : r.GetString(6),
        TeacherFacultyStaffId = r.IsDBNull(7) ? null : r.GetInt32(7),
        TeacherName = r.IsDBNull(8) ? null : r.GetString(8),
        PhotoPath = r.IsDBNull(9) ? null : r.GetString(9),
        ParentGuardian1Name = r.IsDBNull(10) ? null : r.GetString(10),
        ParentGuardian1Phone = r.IsDBNull(11) ? null : r.GetString(11),
        ParentGuardian1Email = r.IsDBNull(12) ? null : r.GetString(12),
        ParentGuardian2Name = r.IsDBNull(13) ? null : r.GetString(13),
        ParentGuardian2Phone = r.IsDBNull(14) ? null : r.GetString(14),
        ParentGuardian2Email = r.IsDBNull(15) ? null : r.GetString(15),
        EmergencyContactName = r.IsDBNull(16) ? null : r.GetString(16),
        EmergencyContactPhone = r.IsDBNull(17) ? null : r.GetString(17),
        EmergencyContactRelationship = r.IsDBNull(18) ? null : r.GetString(18),
        AllergiesMedicalNotes = r.IsDBNull(19) ? null : r.GetString(19),
        AuthorizedPickupNotes = r.IsDBNull(20) ? null : r.GetString(20),
        Notes = r.IsDBNull(21) ? null : r.GetString(21),
        IsActive = r.GetBoolean(22)
    };

    private static FacultyStaffRow ReadFacultyStaff(SqlDataReader r) => new FacultyStaffRow
    {
        FacultyStaffId = r.GetInt32(0),
        FirstName = r.GetString(1),
        LastName = r.GetString(2),
        PreferredName = r.IsDBNull(3) ? null : r.GetString(3),
        RoleTitle = r.IsDBNull(4) ? null : r.GetString(4),
        Department = r.IsDBNull(5) ? null : r.GetString(5),
        Classroom = r.IsDBNull(6) ? null : r.GetString(6),
        Phone = r.IsDBNull(7) ? null : r.GetString(7),
        Extension = r.IsDBNull(8) ? null : r.GetString(8),
        Email = r.IsDBNull(9) ? null : r.GetString(9),
        PhotoPath = r.IsDBNull(10) ? null : r.GetString(10),
        EmergencyContactName = r.IsDBNull(11) ? null : r.GetString(11),
        EmergencyContactPhone = r.IsDBNull(12) ? null : r.GetString(12),
        EmergencyContactRelationship = r.IsDBNull(13) ? null : r.GetString(13),
        Notes = r.IsDBNull(14) ? null : r.GetString(14),
        IsActive = r.GetBoolean(15)
    };

    private static void AddStudentParameters(SqlCommand cmd, StudentRow m)
    {
        cmd.Parameters.AddWithValue("@FirstName", m.FirstName.Trim());
        cmd.Parameters.AddWithValue("@LastName", m.LastName.Trim());
        cmd.Parameters.AddWithValue("@PreferredName", string.IsNullOrWhiteSpace(m.PreferredName) ? DBNull.Value : m.PreferredName.Trim());
        cmd.Parameters.AddWithValue("@DateOfBirth", m.DateOfBirth.HasValue ? m.DateOfBirth.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@GradeLevel", string.IsNullOrWhiteSpace(m.GradeLevel) ? DBNull.Value : m.GradeLevel.Trim());
        cmd.Parameters.AddWithValue("@Classroom", string.IsNullOrWhiteSpace(m.Classroom) ? DBNull.Value : m.Classroom.Trim());
        cmd.Parameters.AddWithValue("@TeacherFacultyStaffId", m.TeacherFacultyStaffId.HasValue ? m.TeacherFacultyStaffId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@PhotoPath", string.IsNullOrWhiteSpace(m.PhotoPath) ? DBNull.Value : m.PhotoPath.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian1Name", string.IsNullOrWhiteSpace(m.ParentGuardian1Name) ? DBNull.Value : m.ParentGuardian1Name.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian1Phone", string.IsNullOrWhiteSpace(m.ParentGuardian1Phone) ? DBNull.Value : m.ParentGuardian1Phone.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian1Email", string.IsNullOrWhiteSpace(m.ParentGuardian1Email) ? DBNull.Value : m.ParentGuardian1Email.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian2Name", string.IsNullOrWhiteSpace(m.ParentGuardian2Name) ? DBNull.Value : m.ParentGuardian2Name.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian2Phone", string.IsNullOrWhiteSpace(m.ParentGuardian2Phone) ? DBNull.Value : m.ParentGuardian2Phone.Trim());
        cmd.Parameters.AddWithValue("@ParentGuardian2Email", string.IsNullOrWhiteSpace(m.ParentGuardian2Email) ? DBNull.Value : m.ParentGuardian2Email.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactName", string.IsNullOrWhiteSpace(m.EmergencyContactName) ? DBNull.Value : m.EmergencyContactName.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactPhone", string.IsNullOrWhiteSpace(m.EmergencyContactPhone) ? DBNull.Value : m.EmergencyContactPhone.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactRelationship", string.IsNullOrWhiteSpace(m.EmergencyContactRelationship) ? DBNull.Value : m.EmergencyContactRelationship.Trim());
        cmd.Parameters.AddWithValue("@AllergiesMedicalNotes", string.IsNullOrWhiteSpace(m.AllergiesMedicalNotes) ? DBNull.Value : m.AllergiesMedicalNotes.Trim());
        cmd.Parameters.AddWithValue("@AuthorizedPickupNotes", string.IsNullOrWhiteSpace(m.AuthorizedPickupNotes) ? DBNull.Value : m.AuthorizedPickupNotes.Trim());
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(m.Notes) ? DBNull.Value : m.Notes.Trim());
        cmd.Parameters.AddWithValue("@IsActive", m.IsActive);
    }

    private static void AddFacultyStaffParameters(SqlCommand cmd, FacultyStaffRow m)
    {
        cmd.Parameters.AddWithValue("@FirstName", m.FirstName.Trim());
        cmd.Parameters.AddWithValue("@LastName", m.LastName.Trim());
        cmd.Parameters.AddWithValue("@PreferredName", string.IsNullOrWhiteSpace(m.PreferredName) ? DBNull.Value : m.PreferredName.Trim());
        cmd.Parameters.AddWithValue("@RoleTitle", string.IsNullOrWhiteSpace(m.RoleTitle) ? DBNull.Value : m.RoleTitle.Trim());
        cmd.Parameters.AddWithValue("@Department", string.IsNullOrWhiteSpace(m.Department) ? DBNull.Value : m.Department.Trim());
        cmd.Parameters.AddWithValue("@Classroom", string.IsNullOrWhiteSpace(m.Classroom) ? DBNull.Value : m.Classroom.Trim());
        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(m.Phone) ? DBNull.Value : m.Phone.Trim());
        cmd.Parameters.AddWithValue("@Extension", string.IsNullOrWhiteSpace(m.Extension) ? DBNull.Value : m.Extension.Trim());
        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(m.Email) ? DBNull.Value : m.Email.Trim());
        cmd.Parameters.AddWithValue("@PhotoPath", string.IsNullOrWhiteSpace(m.PhotoPath) ? DBNull.Value : m.PhotoPath.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactName", string.IsNullOrWhiteSpace(m.EmergencyContactName) ? DBNull.Value : m.EmergencyContactName.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactPhone", string.IsNullOrWhiteSpace(m.EmergencyContactPhone) ? DBNull.Value : m.EmergencyContactPhone.Trim());
        cmd.Parameters.AddWithValue("@EmergencyContactRelationship", string.IsNullOrWhiteSpace(m.EmergencyContactRelationship) ? DBNull.Value : m.EmergencyContactRelationship.Trim());
        cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(m.Notes) ? DBNull.Value : m.Notes.Trim());
        cmd.Parameters.AddWithValue("@IsActive", m.IsActive);
    }

    private async Task WriteSchoolAuditLogAsync(string username, string actionType, string entityType, int entityId, string description)
    {
        await using var conn = CreateConnection();

        const string sql = @"
            INSERT INTO dbo.AuditLog (UserId, ActionType, EntityType, EntityId, Description)
            SELECT TOP 1 UserId, @ActionType, @EntityType, @EntityId, @Description
            FROM dbo.Users
            WHERE Username = @Username
            UNION ALL
            SELECT NULL, @ActionType, @EntityType, @EntityId, @Description
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@ActionType", actionType);
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityId", entityId);
        cmd.Parameters.AddWithValue("@Description", description);

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}
