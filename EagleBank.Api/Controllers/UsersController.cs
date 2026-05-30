using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Users;
using EagleBank.Api.Mappings;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EagleBank.Api.Controllers;

[ApiController]
[Route("v1/users")]
public class UsersController(IUserService userService, ILogger<UsersController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        logger.LogInformation("Received create user request");

        var user = await userService.CreateUserAsync(
            request.Name,
            MapToAddress(request.Address),
            request.PhoneNumber,
            request.Email,
            request.Password);

        logger.LogInformation("Returning 201 for created user {UserId}", user.Id);

        return StatusCode(201, user.ToResponse());
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        logger.LogInformation("Received get user request {UserId}", userId);

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
