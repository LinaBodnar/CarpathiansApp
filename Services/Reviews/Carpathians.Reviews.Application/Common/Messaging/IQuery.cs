using MediatR;

namespace Carpathians.Reviews.Application.Common.Messaging;

/// <summary>Запит — операція читання, що не змінює стан системи.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
