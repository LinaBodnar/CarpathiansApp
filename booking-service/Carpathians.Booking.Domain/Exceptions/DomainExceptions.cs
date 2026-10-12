namespace Carpathians.Domain.Exceptions;

public class NotFoundException(string message) : Exception(message);
public class BusinessConflictException(string message) : Exception(message);
public class ValidationException(string message) : Exception(message);