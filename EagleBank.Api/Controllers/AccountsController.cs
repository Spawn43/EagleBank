using System.Security.Claims;
using System.Text.RegularExpressions;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Accounts;
using EagleBank.Api.Mappings;
using EagleBank.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EagleBank.Api.Controllers;

[ApiController]
[Route("v1/accounts")]
[Authorize]
public class AccountsController(IBankAccountService bankAccountService, ILogger<AccountsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateBankAccountRequest request)
    {
        logger.LogInformation("Received create bank account request");

        var userId = GetAuthenticatedUserId()!;
        var account = await bankAccountService.CreateAccountAsync(userId, request.Name, request.AccountType!.Value);

        logger.LogInformation("Returning 201 for created bank account {AccountNumber}", account.AccountNumber);

        return StatusCode(201, account.ToResponse());
    }

    [HttpGet]
    public async Task<IActionResult> ListAccounts()
    {
        logger.LogInformation("Received list bank accounts request");

        var userId = GetAuthenticatedUserId()!;
        var accounts = await bankAccountService.ListAccountsAsync(userId);

        var response = new ListBankAccountsResponse
        {
            Accounts = accounts.Select(a => a.ToResponse()).ToList()
        };

        logger.LogInformation("Returning 200 with {Count} bank accounts", response.Accounts.Count);

        return Ok(response);
    }

    [HttpGet("{accountNumber}")]
    public async Task<IActionResult> GetAccount(string accountNumber)
    {
        logger.LogInformation("Received get bank account request {AccountNumber}", accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details =
                [
                    new ValidationErrorDetail
                    {
                        Field = "accountNumber",
                        Message = "Account number must match the format 01XXXXXX",
                        Type = "validation_error"
                    }
                ]
            });
        }

        var account = await bankAccountService.GetAccountAsync(accountNumber);

        if (account is null)
        {
            logger.LogInformation("Returning 404 for bank account {AccountNumber}", accountNumber);
            return NotFound(new ErrorResponse { Message = $"Bank account {accountNumber} not found" });
        }

        if (account.UserId != GetAuthenticatedUserId())
        {
            logger.LogWarning("Returning 403 - user {AuthenticatedUserId} attempted to access account {AccountNumber}",
                GetAuthenticatedUserId(), accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }

        logger.LogInformation("Returning 200 for bank account {AccountNumber}", accountNumber);

        return Ok(account.ToResponse());
    }

    [HttpPatch("{accountNumber}")]
    public async Task<IActionResult> UpdateAccount(string accountNumber, [FromBody] UpdateBankAccountRequest request)
    {
        logger.LogInformation("Received update bank account request {AccountNumber}", accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details =
                [
                    new ValidationErrorDetail
                    {
                        Field = "accountNumber",
                        Message = "Account number must match the format 01XXXXXX",
                        Type = "validation_error"
                    }
                ]
            });
        }

        var userId = GetAuthenticatedUserId()!;

        try
        {
            var account = await bankAccountService.UpdateAccountAsync(accountNumber, request.Name, request.AccountType, userId);

            logger.LogInformation("Returning 200 for updated bank account {AccountNumber}", accountNumber);

            return Ok(account.ToResponse());
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Returning 403 - user {UserId} attempted to update account {AccountNumber}", userId, accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for bank account {AccountNumber}", accountNumber);
            return NotFound(new ErrorResponse { Message = $"Bank account {accountNumber} not found" });
        }
    }

    [HttpDelete("{accountNumber}")]
    public async Task<IActionResult> DeleteAccount(string accountNumber)
    {
        logger.LogInformation("Received delete bank account request {AccountNumber}", accountNumber);

        if (!Regex.IsMatch(accountNumber, @"^01\d{6}$"))
        {
            logger.LogWarning("Returning 400 - invalid account number format {AccountNumber}", accountNumber);
            return BadRequest(new BadRequestErrorResponse
            {
                Message = "Invalid account number format",
                Details =
                [
                    new ValidationErrorDetail
                    {
                        Field = "accountNumber",
                        Message = "Account number must match the format 01XXXXXX",
                        Type = "validation_error"
                    }
                ]
            });
        }

        var userId = GetAuthenticatedUserId()!;

        try
        {
            await bankAccountService.DeleteAccountAsync(accountNumber, userId);

            logger.LogInformation("Returning 204 for deleted bank account {AccountNumber}", accountNumber);

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Returning 403 - user {UserId} attempted to delete account {AccountNumber}", userId, accountNumber);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this bank account" });
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for bank account {AccountNumber}", accountNumber);
            return NotFound(new ErrorResponse { Message = $"Bank account {accountNumber} not found" });
        }
    }

    private string? GetAuthenticatedUserId() => User.FindFirstValue("sub");
}
