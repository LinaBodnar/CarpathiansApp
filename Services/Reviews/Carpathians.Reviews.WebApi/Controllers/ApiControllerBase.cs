using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Reviews.WebApi.Controllers;

/// <summary>
/// Базовий контролер: доступ до MediatR (<see cref="ISender"/>). Контролери лише перетворюють
/// HTTP-запит на команду або запит і передають його в MediatR — без бізнес-логіки й без доступу до БД.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
