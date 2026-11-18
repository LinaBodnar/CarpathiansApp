using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Carpathians.Catalog.Tests.IntegrationEvents;

/// <summary>
/// Межі транзакції RouteService (Лаб. 08): подія фіксується в тій самій транзакції, що й зміни,
/// і публікується лише після успішного коміту.
/// </summary>
[TestFixture]
public class RouteServiceEventsTests
{
    private IUnitOfWork _uow = null!;
    private IRouteRepository _routes = null!;
    private ICatalogIntegrationEventService _events = null!;
    private RouteService _service = null!;
    private TrailRoute _route = null!;

    [SetUp]
    public void SetUp()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _routes = Substitute.For<IRouteRepository>();
        _uow.Routes.Returns(_routes);
        _events = Substitute.For<ICatalogIntegrationEventService>();
        _service = new RouteService(_uow, Substitute.For<IMapper>(), _events);

        _route = new TrailRoute(1, "Говерла", "hoverla", Difficulty.Moderate, 8.5m, 6m, 1200);
        _routes.GetWithDetailsEagerAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_route);
        _routes.GetByIdAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(_route);
    }

    [TearDown]
    public async Task TearDown() => await _uow.DisposeAsync();

    [Test]
    public async Task Rename_SavesEventInTransaction_AndPublishesAfterCommit()
    {
        await _service.UpdateAsync(1, Update("Говерла (2061 м)"));

        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync(Arg.Any<System.Data.IsolationLevel>(), Arg.Any<CancellationToken>());
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _events.AddRouteRenamedAsync(_route, "Говерла", Arg.Any<CancellationToken>());
            _uow.CommitAsync(Arg.Any<CancellationToken>());
            _events.PublishPendingAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task UpdateWithoutRename_DoesNotProduceEvents()
    {
        await _service.UpdateAsync(1, Update("Говерла"));

        await _events.DidNotReceiveWithAnyArgs().AddRouteRenamedAsync(default!, default!);
        await _events.DidNotReceiveWithAnyArgs().PublishPendingAsync();
        await _uow.DidNotReceiveWithAnyArgs().BeginTransactionAsync();
    }

    [Test]
    public void FailedCommit_RollsBack_AndDoesNotPublish()
    {
        _uow.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("DB is down"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(1, Update("Нова назва")));

        _uow.Received().RollbackAsync(Arg.Any<CancellationToken>());
        _events.DidNotReceiveWithAnyArgs().PublishPendingAsync();
    }

    [Test]
    public async Task Delete_PublishesRouteDeleted()
    {
        await _service.DeleteAsync(1, "admin");

        Received.InOrder(() =>
        {
            _events.AddRouteDeletedAsync(_route, Arg.Any<CancellationToken>());
            _uow.CommitAsync(Arg.Any<CancellationToken>());
            _events.PublishPendingAsync(Arg.Any<CancellationToken>());
        });
        Assert.That(_route.IsDeleted, Is.True);
    }

    private static UpdateRouteDto Update(string name)
        => new(name, Difficulty.Moderate, 8.5m, 6m, 1200, "editor");
}
