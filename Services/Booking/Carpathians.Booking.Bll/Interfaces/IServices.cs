using Carpathians.Bll.Dtos;
namespace Carpathians.Bll.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct);
    Task<CustomerDto> CreateAsync(CustomerInputDto input, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(long id, CustomerInputDto input, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IDepartureService
{
    Task<DepartureDto> GetByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<DepartureDto>> GetAllAsync(CancellationToken ct);
    Task<DepartureDto> CreateAsync(CreateDepartureDto input, CancellationToken ct);
    Task<DepartureDto> UpdateAsync(long id, UpdateDepartureDto input, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IBookingService
{
    Task<BookingDto> GetByIdAsync(long id, CancellationToken ct);
    Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct);   
    Task ConfirmAsync(long id, CancellationToken ct);                             
    Task<PaymentResultDto> PayAsync(long id, PayBookingDto input, CancellationToken ct);
    Task CancelAsync(long id, CancellationToken ct);
}