using System.ComponentModel.DataAnnotations;

namespace EagleBank.Api.DTOs.Users;

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public AddressDto? Address { get; set; }

    [RegularExpression(@"^\+[1-9]\d{1,14}$", ErrorMessage = "Phone number must be in E.164 format")]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}
