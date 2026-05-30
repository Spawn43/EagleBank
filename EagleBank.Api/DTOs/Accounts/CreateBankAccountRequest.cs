using System.ComponentModel.DataAnnotations;
using EagleBank.Domain.Models;

namespace EagleBank.Api.DTOs.Accounts;

public class CreateBankAccountRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public AccountType? AccountType { get; set; }
}
    