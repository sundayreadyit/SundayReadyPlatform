namespace ChurchAssetTracker.Data;

public class StudentRow
{
    public int StudentId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PreferredName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? GradeLevel { get; set; }
    public string? Classroom { get; set; }
    public int? TeacherFacultyStaffId { get; set; }
    public string? TeacherName { get; set; }
    public string? PhotoPath { get; set; }
    public string? ParentGuardian1Name { get; set; }
    public string? ParentGuardian1Phone { get; set; }
    public string? ParentGuardian1Email { get; set; }
    public string? ParentGuardian2Name { get; set; }
    public string? ParentGuardian2Phone { get; set; }
    public string? ParentGuardian2Email { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? AllergiesMedicalNotes { get; set; }
    public string? AuthorizedPickupNotes { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public string DisplayName
    {
        get
        {
            var first = string.IsNullOrWhiteSpace(PreferredName) ? FirstName : PreferredName;
            return $"{first} {LastName}".Trim();
        }
    }
}

public class FacultyStaffRow
{
    public int FacultyStaffId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PreferredName { get; set; }
    public string? RoleTitle { get; set; }
    public string? Department { get; set; }
    public string? Classroom { get; set; }
    public string? Phone { get; set; }
    public string? Extension { get; set; }
    public string? Email { get; set; }
    public string? PhotoPath { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public string DisplayName
    {
        get
        {
            var first = string.IsNullOrWhiteSpace(PreferredName) ? FirstName : PreferredName;
            return $"{first} {LastName}".Trim();
        }
    }
}

public class StudentFormViewModel
{
    public StudentRow Student { get; set; } = new();
    public List<FacultyStaffRow> Teachers { get; set; } = new();
    public List<StudentDocumentRow> Documents { get; set; } = new();
}

public class StudentRosterViewModel
{
    public int? TeacherFacultyStaffId { get; set; }
    public string? GradeLevel { get; set; }
    public List<FacultyStaffRow> Teachers { get; set; } = new();
    public List<string> GradeLevels { get; set; } = new();
    public List<StudentRow> Students { get; set; } = new();

    public string HeaderTitle
    {
        get
        {
            var teacher = Teachers.FirstOrDefault(t => t.FacultyStaffId == TeacherFacultyStaffId);

            if (teacher != null)
                return $"{teacher.DisplayName}'s Class";

            if (!string.IsNullOrWhiteSpace(GradeLevel))
                return $"Grade {GradeLevel} Student Roster";

            return "Student Roster";
        }
    }
}


public class StudentDocumentRow
{
    public int StudentDocumentId { get; set; }
    public int StudentId { get; set; }
    public string DocumentType { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Notes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes >= 1024 * 1024)
                return $"{FileSizeBytes / 1024d / 1024d:0.0} MB";

            if (FileSizeBytes >= 1024)
                return $"{FileSizeBytes / 1024d:0.0} KB";

            return $"{FileSizeBytes} bytes";
        }
    }
}
