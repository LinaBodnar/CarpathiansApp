var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword, port: 14333)
    .WithDataVolume();

var bookingDb = sql.AddDatabase("BookingDb", "BookingContext");

builder.AddProject<Projects.Carpathians_Booking_Api>("booking-api")
    .WithReference(bookingDb)
    .WaitFor(bookingDb);

builder.Build().Run();