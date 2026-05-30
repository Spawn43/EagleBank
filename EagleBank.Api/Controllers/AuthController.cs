using EagleBank.Api.DTOs;
using EagleBank.Api.DTOs.Auth;
using EagleBank.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EagleBank.Api.Controllers;

[ApiController]
[Route("v1/auth")]
[AllowAnonymous]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] AuthRequest request)
    {
        logger.LogInformation("Authentication request received");

        var result = await authService.AuthenticateAsync(request.Email, request.Password);

        if (result is null)
        {
            logger.LogInformation("Returning 401 - invalid credentials");
            return Unauthorized(new ErrorResponse { Message = "Invalid credentials" });
        }

        logger.LogInformation("Returning 200 - authentication successful");
        return Ok(new AuthResponse { Token = result.Token });
    }
}
