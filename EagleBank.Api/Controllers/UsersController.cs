using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using EagleBank.Api.Mappings;
using EagleBank.Domain.Exceptions;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EagleBank.Api.Controllers;

[ApiController]
[Route("v1/users")]
[Authorize]
public class UsersController(IUserService userService, ILogger<UsersController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        logger.LogInformation("Received create user request");

        try
        {
            var user = await userService.CreateUserAsync(
                request.Name,
                MapToAddress(request.Address),
                request.PhoneNumber,
                request.Email,
                request.Password);

            logger.LogInformation("Returning 201 for created user {UserId}", user.Id);

            return StatusCode(201, user.ToResponse());
        }
        catch (DuplicateEmailException)
        {
            logger.LogInformation("Returning 409 - email already registered");
            return StatusCode(409, new ErrorResponse { Message = "A user with this email address already exists" });
        }
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        logger.LogInformation("Received get user request {UserId}", userId);

        if (!IsOwner(userId))
        {
            logger.LogWarning("Returning 403 - user {AuthenticatedUserId} attempted to access user {UserId}",
                GetAuthenticatedUserId(), userId);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this user" });
        }

        var user = await userService.GetUserAsync(userId);

        if (user is null)
        {
            logger.LogInformation("Returning 404 for user {UserId}", userId);
            return NotFound(new ErrorResponse { Message = $"User {userId} not found" });
        }

        logger.LogInformation("Returning 200 for user {UserId}", userId);

        return Ok(user.ToResponse());
    }

    [HttpPatch("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
    {
        logger.LogInformation("Received update user request {UserId}", userId);

        if (!IsOwner(userId))
        {
            logger.LogWarning("Returning 403 - user {AuthenticatedUserId} attempted to update user {UserId}",
                GetAuthenticatedUserId(), userId);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this user" });
        }

        try
        {
            var address = request.Address is not null ? MapToAddress(request.Address) : null;

            var user = await userService.UpdateUserAsync(
                userId,
                request.Name,
                address,
                request.PhoneNumber,
                request.Email);

            logger.LogInformation("Returning 200 for updated user {UserId}", userId);

            return Ok(user.ToResponse());
        }
        catch (DuplicateEmailException)
        {
            logger.LogInformation("Returning 409 - email already registered");
            return StatusCode(409, new ErrorResponse { Message = "A user with this email address already exists" });
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for user {UserId}", userId);
            return NotFound(new ErrorResponse { Message = $"User {userId} not found" });
        }
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        logger.LogInformation("Received delete user request {UserId}", userId);

        if (!IsOwner(userId))
        {
            logger.LogWarning("Returning 403 - user {AuthenticatedUserId} attempted to delete user {UserId}",
                GetAuthenticatedUserId(), userId);
            return StatusCode(403, new ErrorResponse { Message = "You are not authorised to access this user" });
        }

        try
        {
            await userService.DeleteUserAsync(userId);

            logger.LogInformation("Returning 204 for deleted user {UserId}", userId);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            logger.LogInformation("Returning 404 for user {UserId}", userId);
            return NotFound(new ErrorResponse { Message = $"User {userId} not found" });
        }
    }

    private bool IsOwner(string userId) => GetAuthenticatedUserId() == userId;

    // MapInboundClaims = false in Program.cs keeps JWT claim names as-is,
    // so "sub" stays "sub" rather than being remapped to ClaimTypes.NameIdentifier
    private string? GetAuthenticatedUserId() => User.FindFirstValue("sub");

    private static Address MapToAddress(AddressDto dto) => new()
    {
        Line1 = dto.Line1,
        Line2 = dto.Line2,
        Line3 = dto.Line3,
        Town = dto.Town,
        County = dto.County,
        Postcode = dto.Postcode
    };
}
