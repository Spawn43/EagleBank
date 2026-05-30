namespace EagleBank.Domain.Exceptions;

public class DuplicateEmailException() : Exception("A user with this email address already exists");
