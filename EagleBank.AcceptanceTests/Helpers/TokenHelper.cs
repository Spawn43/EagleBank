using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EagleBank.AcceptanceTests.Helpers;

/// <summary>
/// Generates valid JWTs directly, bypassing the API auth endpoint.
/// Used to simulate a user who holds a token obtained before the DB went down.
/// Values must match appsettings.json JwtSettings.
/// </summary>
public static class TokenHelper
{
    private const string Secret = "eaglebank-super-secret-jwt-key-min-32-chars!";
    private const string Issuer = "eaglebank-api";
    private const string Audience = "eaglebank-client";

    public static string GenerateTokenForUser(string userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
