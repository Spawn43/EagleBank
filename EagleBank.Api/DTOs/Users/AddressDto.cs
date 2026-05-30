using System.ComponentModel.DataAnnotations;

namespace EagleBank.Api.DTOs.Users;

public class AddressDto
{
    [Required]
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string? Line3 { get; set; }
    [Required]
    public string Town { get; set; } = string.Empty;
    [Required]
    public string County { get; set; } = string.Empty;
    [Required]
    public string Postcode { get; set; } = string.Empty;
}
