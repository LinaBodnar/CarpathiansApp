using Carpathians.Platform.EventBus.Consuming;
using Carpathians.Platform.EventBus.Messaging;
using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;
using Carpathians.Reviews.WebApi.IntegrationEvents.Events;
using Carpathians.Reviews.WebApi.IntegrationEvents.Handlers;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Carpathians.Reviews.Tests.WebApi;

/// <summary>Обробник route.renamed (Лаб. 08): routeName у відгуках, ідемпотентність, out-of-order, DLQ.</summary>
[TestFixture]
public class RouteRenamedHandlerTests
{
    private IIntegrationEventStore _store = null!;
    private ISender _mediator = null!;
    private RouteRenamedHandler _handler = null!;
    private RouteRenamedIntegrationEvent _event = null!;

    [SetUp]
    public void SetUp()
    {
        _store = Substitute.For<IIntegrationEventStore>();
        _mediator = Substitute.For<ISender>();
        _handler = new RouteRenamedHandler(_store, _mediator, NullLogger<RouteRenamedHandler>.Instance);
        _event = new RouteRenamedIntegrationEvent(7, "Говерла", "Говерла (2061 м)") { CorrelationId = "corr-1" };

        _store.TryAdvanceRouteNameAsync(default, default!, default, default).ReturnsForAnyArgs(true);
    }

    [Test]
    public async Task NewEvent_UpdatesRouteNameInReviews_ThenMarksProcessed()
    {
        await _handler.HandleAsync(_event, CancellationToken.None);

        Received.InOrder(() =>
        {
            _mediator.Send(Arg.Is<SyncRouteNameCommand>(c => c.RouteId == 7 && c.RouteName == "Говерла (2061 м)"),
                Arg.Any<CancellationToken>());
            _store.MarkProcessedAsync(_event.EventId, nameof(RouteRenamedIntegrationEvent), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task DuplicateEvent_IsSkipped()
    {
        _store.IsProcessedAsync(_event.EventId, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.HandleAsync(_event, CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SyncRouteNameCommand>(), default);
    }

    [Test]
    public async Task OutOfOrderEvent_DoesNotOverwriteNewerName()
    {
        _store.TryAdvanceRouteNameAsync(default, default!, default, default).ReturnsForAnyArgs(false);

        await _handler.HandleAsync(_event, CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<SyncRouteNameCommand>(), default);
        await _store.Received().MarkProcessedAsync(_event.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void InvalidEventData_IsPermanentFailure()
    {
        _mediator.Send(Arg.Any<SyncRouteNameCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException([new ValidationFailure("RouteName", "too long")]));

        Assert.ThrowsAsync<PermanentEventException>(() => _handler.HandleAsync(_event, CancellationToken.None));
        _store.DidNotReceiveWithAnyArgs().MarkProcessedAsync(default, default!);
    }

    [Test]
    public void EventJson_IsCamelCase_AndRoundTrips()
    {
        var json = System.Text.Encoding.UTF8.GetString(EventJson.Serialize(_event));

        Assert.That(json, Does.Contain("\"eventId\"").And.Contain("\"newName\"").And.Contain("\"correlationId\":\"corr-1\""));
        Assert.That(json, Does.Not.Contain("schemaVersion"));
        Assert.That(EventJson.Deserialize<RouteRenamedIntegrationEvent>(EventJson.Serialize(_event)), Is.EqualTo(_event));
    }
}
