using MediatR;

namespace Carpathians.Reviews.Application.Common.Messaging;

/// <summary>Команда — операція запису (створення, зміна, видалення) без результату.</summary>
public interface ICommand : IRequest
{
}

/// <summary>Команда — операція запису, що повертає результат (наприклад, Id створеного документа).</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
