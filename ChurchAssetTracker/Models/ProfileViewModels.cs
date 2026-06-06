using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class UserProfileViewModel
{
    public int UserId { get; set; }

    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = "";

    public string Username { get; set; } = "";

    [EmailAddress]
    public string? Email { get; set; }

    public string? ProfilePicturePath { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}