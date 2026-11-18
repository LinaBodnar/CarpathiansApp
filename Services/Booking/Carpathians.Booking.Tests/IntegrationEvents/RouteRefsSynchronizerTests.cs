using Carpathians.Api.IntegrationEvents;
using Carpathians.Api.IntegrationEvents.Events;
using Carpathians.Api.IntegrationEvents.Handlers;
using Carpathians.Dal.Interfaces;
using Carpathians.Platform.EventBus.Consuming;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Carpathians.Booking.Tests.IntegrationEvents;

/// <summary>
/// Обробники подій Каталогу в Бронюванні (Лаб. 08): ідемпотентність через ProcessedEvents у тій самій транзакції,
/// оновлення копії RouteRefs, постійні й тимчасові збої.
/// </summary>
[TestFixture]
public class RouteRefsSynchronizerTests
{
    private IUnitOfWork _uow = null!;
    private IProcessedEventRepository _processed = null!;
    private IRouteRefRepository _routeRefs = null!;
    private RouteRefsSynchronizer _sync = null!;

    [SetUp]
    public void SetUp()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _processed = Substitute.For<IProcessedEventRepository>();
        _routeRefs = Substitute.For<IRouteRefRepository>();
        _uow.ProcessedEvents.Returns(_processed);
        _uow.RouteRefs.Returns(_routeRefs);
        _sync = new RouteRefsSynchronizer(_uow, NullLogger<RouteRefsSynchronizer>.Instance);

        _processed.TryAddAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _routeRefs.ApplyAsync(default, default!, default, default, default).ReturnsForAnyArgs(true);
    }

    [TearDown]
    public async Task TearDown() => await _uow.DisposeAsync();

    [Test]
    public async Task Renamed_RecordsEventAndUpdatesCopy_InOneTransaction()
    {
        var e = new RouteRenamedIntegrationEvent(7, "Говерла", "Говерла (2061 м)");

        await new RouteRenamedHandler(_sync).HandleAsync(e, CancellationToken.None);

        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync(Arg.Any<System.Data.IsolationLevel>(), Arg.Any<CancellationToken>());
            _processed.TryAddAsync(e.EventId, nameof(RouteRenamedIntegrationEvent), Arg.Any<CancellationToken>());
            _routeRefs.ApplyAsync(7, "Говерла (2061 м)", true, e.OccurredAt, Arg.Any<CancellationToken>());
            _uow.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task DuplicateEvent_IsSkipped_WithoutChangingData()
    {
        _processed.TryAddAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await new RouteRenamedHandler(_sync).HandleAsync(
            new RouteRenamedIntegrationEvent(7, "Говерла", "Нова назва"), CancellationToken.None);

        await _routeRefs.DidNotReceiveWithAnyArgs().ApplyAsync(default, default!, default, default, default);
        await _uow.Received().RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceiveWithAnyArgs().CommitAsync();
    }

    [Test]
    public async Task Deleted_MarksCopyInactive()
    {
        var e = new RouteDeletedIntegrationEvent(7, "Говерла");

        await new RouteDeletedHandler(_sync).HandleAsync(e, CancellationToken.None);

        await _routeRefs.Received().ApplyAsync(7, "Говерла", false, e.OccurredAt, Arg.Any<CancellationToken>());
    }

    [Test]
    public void InvalidData_IsPermanentFailure_ForDlq()
    {
        Assert.ThrowsAsync<PermanentEventException>(() => new RouteCreatedHandler(_sync).HandleAsync(
            new RouteCreatedIntegrationEvent(7, " ", "slug", 1, "Hard"), CancellationToken.None));

        _uow.DidNotReceiveWithAnyArgs().BeginTransactionAsync();
    }

    [Test]
    public void DatabaseFailure_RollsBack_AndPropagatesForRetry()
    {
        _routeRefs.ApplyAsync(default, default!, default, default, default)
            .ThrowsAsyncForAnyArgs(new TimeoutException("SQL Server is unavailable"));

        Assert.ThrowsAsync<TimeoutException>(() => new RouteCreatedHandler(_sync).HandleAsync(
            new RouteCreatedIntegrationEvent(7, "Говерла", "hoverla", 1, "Hard"), CancellationToken.None));

        _uow.Received().RollbackAsync(Arg.Any<CancellationToken>());
        _uow.DidNotReceiveWithAnyArgs().CommitAsync();
    }
}
