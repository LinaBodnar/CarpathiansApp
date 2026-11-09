// Orchestrator платформи Carpathians: лише конфігурація ресурсів, без бізнес-логіки.
var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
