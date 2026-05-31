namespace EagleBank.Api.DTOs.Transactions;

public class ListTransactionsResponse
{
    public List<TransactionResponse> Transactions { get; set; } = [];
}
