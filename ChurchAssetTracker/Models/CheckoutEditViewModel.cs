using System.ComponentModel.DataAnnotations;

namespace ChurchAssetTracker.Models;

public class CheckoutCreateViewModel
{
    [Required(ErrorMessage = "Asset is required")]
    public int AssetId { get; set; }

    [Required(ErrorMessage = "Borrower is required")]
    public int PersonId { get; set; }

    [Range(1, 100000, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityOut { get; set; } = 1;

    [DataType(DataType.Date)]
    public DateTime? ExpectedReturnDate { get; set; }

    [StringLength(255)]
    public string? ConditionOut { get; set; }

    public string? CheckoutNotes { get; set; }

    public List<OptionItem> AvailableAssets { get; set; } = new();
    public List<OptionItem> ActivePeople { get; set; } = new();
}

public class CheckoutReturnViewModel
{
    public int CheckoutId { get; set; }
    public string AssetName { get; set; } = "";
    public string Borrower { get; set; } = "";
    public int QuantityOut { get; set; }
    public DateTime CheckoutDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }

    [StringLength(255)]
    public string? ConditionReturned { get; set; }

    public string? ReturnNotes { get; set; }
}

public class OptionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
