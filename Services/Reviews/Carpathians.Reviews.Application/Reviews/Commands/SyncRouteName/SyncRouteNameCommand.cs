using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;

/// <summary>
/// Оновлення денормалізованої назви маршруту (reviews.routeName, discussions.routeName)
/// за подією з Каталогу — власника даних. Повертає кількість оновлених документів.
/// </summary>
public record SyncRouteNameCommand(long RouteId, string RouteName) : ICommand<long>;
