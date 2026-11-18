namespace Carpathians.Reviews.Infrastructure.Persistence;

/// <summary>Налаштування підключення до MongoDB (секція <c>MongoDb</c> в appsettings.json).</summary>
public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    /// <summary>Рядок підключення; пул і таймаути — параметрами (maxPoolSize, serverSelectionTimeoutMS).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "ReviewContext";

    /// <summary>Наповнювати порожню базу тестовими даними під час старту (вимикається в Production).</summary>
    public bool SeedOnStartup { get; set; } = true;
}
