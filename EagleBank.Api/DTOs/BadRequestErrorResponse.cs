namespace EagleBank.Api.DTOs;

public class BadRequestErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public List<ValidationErrorDetail> Details { get; set; } = [];
}

public class ValidationErrorDetail
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
