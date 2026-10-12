using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Constants;
using Carpathians.Domain.Exceptions;
using Carpathians.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace Carpathians.Tests;

[TestFixture]
public class BookingServiceTests
{
    private IUnitOfWork _uow = null!;
    private IBookingRepository _bookings = null!;

    [SetUp]
    public void SetUp()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _bookings = Substitute.For<IBookingRepository>();
        _uow.Bookings.Returns(_bookings);
    }
    
    [TearDown]
    public async Task TearDown()
    {
        await _uow.DisposeAsync();
    }

    private BookingService CreateSut() => new(_uow, Substitute.For<IMapper>());

    [Test]
    public void Mapping_configuration_is_valid()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<BookingProfile>(), NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }

    [Test]
    public async Task Confirm_missing_booking_throws_NotFound_and_rolls_back()
    {
        _bookings.GetWithItemsAsync(999, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(null));

        Assert.ThrowsAsync<NotFoundException>(() => CreateSut().ConfirmAsync(999, default));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Confirm_booking_without_items_throws_Conflict_and_rolls_back()
    {
        _bookings.GetWithItemsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(
                new Booking { Id = 1, Status = BookingStatus.Submitted }));

        Assert.ThrowsAsync<BusinessConflictException>(() => CreateSut().ConfirmAsync(1, default));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_with_duplicate_departures_throws_Validation_before_transaction()
    {
        var input = new CreateBookingDto
        {
            CustomerId = 1,
            ContactPhone = "+380501234567",
            MeetingPoint = "Rakhiv",
            Items =
            [
                new() { DepartureId = 1, Seats = 1 },
                new() { DepartureId = 1, Seats = 2 }
            ]
        };

        Assert.ThrowsAsync<ValidationException>(() => CreateSut().CreateAsync(input, default));

        await _uow.DidNotReceive().BeginTransactionAsync(
            Arg.Any<System.Data.IsolationLevel>(), Arg.Any<CancellationToken>());
    }
}