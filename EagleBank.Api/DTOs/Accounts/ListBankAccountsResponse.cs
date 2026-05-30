namespace EagleBank.Api.DTOs.Accounts;

public class ListBankAccountsResponse
{
    public List<BankAccountResponse> Accounts { get; set; } = [];
}
