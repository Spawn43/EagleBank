using EagleBank.Api.DTOs;
using EagleBank.Api.Middleware;
using EagleBank.Data;
using EagleBank.Data.Repositories;
using EagleBank.Data.Security;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("EagleBankDb"));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
