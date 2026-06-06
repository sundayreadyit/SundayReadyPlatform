using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class UserListItem
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Roles { get; set; } = "";
}

public class RoleOption
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsSelected { get; set; }
}

public class UserEditViewModel
{
    public int UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = "";

    [Required]
    [StringLength(150)]
    public string DisplayName { get; set; } = "";

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public List<int> SelectedRoleIds { get; set; } = new();
    public List<RoleOption> AvailableRoles { get; set; } = new();
}
