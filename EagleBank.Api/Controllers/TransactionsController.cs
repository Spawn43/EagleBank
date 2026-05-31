using System.Security.Claims;
using System.Text.RegularExpressions;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Transactions;
using EagleBank.Api.Mappings;
using EagleBank.Domain.Exceptions;
using EagleBank.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EagleBank.Api.Controllers;

[ApiController]
[Route("v1/accounts/{accountNumber}/transactions")]
[Authorize]
public class TransactionsController(ITransactionService transactionService, ILogger<TransactionsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTransaction(string accountNumber, [FromBody] CreateTransactionRequest request)
    {
        logger.LogInformation("Received create transaction request for account {AccountNumber}", accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details = [new ValidationErrorDetail { Field = "accountNumber", Message = "Account number must match the format 01XXXXXX", Type = "validation_error" }]
            });
        }

        var userId = GetAuthenticatedUserId()!;

        try
        {
            var transaction = await transactionService.CreateTransactionAsync(
                accountNumber, request.Amount, request.Currency!, request.Type!.Value, request.Reference, userId);

            logger.LogInformation("Returning 201 for transaction {TransactionId} on account {AccountNumber}", transaction.Id, accountNumber);

            return StatusCode(201, transaction.ToResponse());
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Returning 403 - user {UserId} attempted to create transaction on account {AccountNumber}", userId, accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for bank account {AccountNumber}", accountNumber);
            return NotFound(new ErrorResponse { Message = $"Bank account {accountNumber} not found" });
        }
        catch (InsufficientFundsException)
        {
            logger.LogWarning("Returning 422 - insufficient funds for withdrawal on account {AccountNumber}", accountNumber);
            return UnprocessableEntity(new ErrorResponse { Message = "Insufficient funds to process this transaction" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListTransactions(string accountNumber)
    {
        logger.LogInformation("Received list transactions request for account {AccountNumber}", accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details = [new ValidationErrorDetail { Field = "accountNumber", Message = "Account number must match the format 01XXXXXX", Type = "validation_error" }]
            });
        }

        var userId = GetAuthenticatedUserId()!;

        try
        {
            var transactions = await transactionService.ListTransactionsAsync(accountNumber, userId);
            var response = new ListTransactionsResponse
            {
                Transactions = transactions.Select(t => t.ToResponse()).ToList()
            };

            logger.LogInformation("Returning 200 with {Count} transactions for account {AccountNumber}", response.Transactions.Count, accountNumber);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Returning 403 - user {UserId} attempted to list transactions on account {AccountNumber}", userId, accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for bank account {AccountNumber}", accountNumber);
            return NotFound(new ErrorResponse { Message = $"Bank account {accountNumber} not found" });
        }
    }

    [HttpGet("{transactionId}")]
    public async Task<IActionResult> GetTransaction(string accountNumber, string transactionId)
    {
        logger.LogInformation("Received get transaction request {TransactionId} for account {AccountNumber}", transactionId, accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details = [new ValidationErrorDetail { Field = "accountNumber", Message = "Account number must match the format 01XXXXXX", Type = "validation_error" }]
            });
        }

        if (!Regex.IsMatch(transactionId, @"^tan-[A-Za-z0-9]+$"))
        {
            logger.LogWarning("Returning 400 - invalid transaction ID format {TransactionId}", transactionId);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid transaction ID format",
                Details = [new ValidationErrorDetail { Field = "transactionId", Message = "Transaction ID must match the format tan-XXXXXXXX", Type = "validation_error" }]
            });
        }

        var userId = GetAuthenticatedUserId()!;

        try
        {
            var transaction = await transactionService.GetTransactionAsync(accountNumber, transactionId, userId);

            logger.LogInformation("Returning 200 for transaction {TransactionId}", transactionId);

            return Ok(transaction.ToResponse());
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Returning 403 - user {UserId} attempted to access transaction {TransactionId} on account {AccountNumber}",
                userId, transactionId, accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogInformation("Returning 404: {Message}", ex.Message);
            return NotFound(new ErrorResponse { Message = ex.Message });
        }
    }

    private string? GetAuthenticatedUserId() => User.FindFirstValue("sub");
}
