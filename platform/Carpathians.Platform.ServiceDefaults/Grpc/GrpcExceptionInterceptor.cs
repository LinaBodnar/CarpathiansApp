using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Carpathians.Platform.ServiceDefaults.Grpc;

/// <summary>
/// Базовий interceptor перетворення доменних винятків сервісу на <see cref="RpcException"/>
/// з відповідним кодом статусу (NOT_FOUND, INVALID_ARGUMENT, FAILED_PRECONDITION, ...), а не загальним INTERNAL.
/// Кожен сервіс задає власну таблицю відповідностей у <see cref="MapException"/> — так само, як
/// обробник винятків REST перетворює їх на HTTP-статуси. gRPC-сервіси лишаються тонкими адаптерами
/// над бізнес-логікою, без try/catch у кожному методі.
/// Реєстрація — після <see cref="GrpcServerLoggingInterceptor"/>, щоб логування бачило вже перетворений статус.
/// </summary>
public abstract class GrpcExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex) when (ex is not RpcException && MapException(ex) is { } status)
        {
            throw new RpcException(new Status(status, ex.Message, ex));
        }
    }

    /// <summary>Код статусу gRPC для доменного винятку; null — виняток неочікуваний (INTERNAL).</summary>
    protected abstract StatusCode? MapException(Exception exception);
}
