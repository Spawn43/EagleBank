using System.Text;
using System.Text.Json.Serialization;
using EagleBank.Api.DTOs;
using EagleBank.Api.Middleware;
using EagleBank.Api.Services;
using EagleBank.Data;
using EagleBank.Data.Repositories;
using EagleBank.Data.Security;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Services;
using EagleBank.Domain.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configuration is resolved in order: appsettings.json → appsettings.{Environment}.json
// → environment variables → any additional providers registered below.
// To read secrets from AWS Parameter Store instead of appsettings, add:
//
//   builder.Configuration.AddSystemsManager("/EagleBank/");   // requires AWSSDK.Extensions.NETCore.Setup
//
// Parameter Store keys map directly to IConfiguration keys, e.g.:
//   /EagleBank/JwtSettings/Secret       → JwtSettings:Secret
//   /EagleBank/ConnectionStrings/DefaultConnection → ConnectionStrings:DefaultConnection
//
// No other code needs to change — all reads below use IConfiguration.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(error => new ValidationErrorDetail
                {
                    Field = e.Key,
                    Message = error.ErrorMessage,
                    Type = "validation_error"
                }))
                .ToList();

            return new BadRequestObjectResult(new BadRequestErrorResponse
            {
                Message = "Invalid details supplied",
                Details = errors
            });
        };
    });

builder.Services.AddOpenApi();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse { Message = "Access token is missing or invalid" });
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("EagleBankDb"));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
builder.Services.Configure<BankAccountSettings>(builder.Configuration.GetSection("BankAccountSettings"));
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
