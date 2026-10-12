using Serilog;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BookingDb")
                       ?? throw new InvalidOperationException("Connection string 'BookingDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.Logger.LogInformation("BookingDb connection string loaded");
app.MapControllers();

app.MapGet("/debug/rollback", async (IUnitOfWork uow, CancellationToken ct) =>
{
    var before = (await uow.Departures.GetByIdAsync(8, ct))!.SeatsAvailable;
    await uow.BeginTransactionAsync(ct: ct);
    await uow.Departures.DecreaseSeatsAsync(8, 1, ct);
    var inTx = (await uow.Departures.GetByIdAsync(8, ct))!.SeatsAvailable;
    await uow.RollbackAsync(ct);
    var after = (await uow.Departures.GetByIdAsync(8, ct))!.SeatsAvailable;
    return new { before, inTx, after };
});

app.Run();