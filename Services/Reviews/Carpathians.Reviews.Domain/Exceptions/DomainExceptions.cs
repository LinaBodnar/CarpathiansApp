namespace Carpathians.Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила (інваріанта) доменної моделі.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Запитаний документ не знайдено.</summary>
public class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity, object id)
        => new($"{entity} '{id}' was not found");
}

/// <summary>Конфлікт стану: документ змінено іншим запитом (конфлікт версій) або дублікат.</summary>
public class ConflictException(string message) : Exception(message);
