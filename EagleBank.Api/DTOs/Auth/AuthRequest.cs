using System.ComponentModel.DataAnnotations;

namespace EagleBank.Api.DTOs.Auth;

public class AuthRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
