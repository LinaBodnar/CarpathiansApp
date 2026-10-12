using Serilog;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Repositories;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BookingDb")
                       ?? throw new InvalidOperationException("Connection string 'BookingDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDepartureService, DepartureService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<BookingProfile>();
});

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

app.Run();