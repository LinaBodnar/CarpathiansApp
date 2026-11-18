using MongoDB.Bson;

namespace Carpathians.Reviews.Domain.Common;

/// <summary>
/// Базова сутність для документів MongoDB: ідентифікатор ObjectId, аудитні поля
/// та номер версії для оптимістичної конкурентності.
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>
    /// Версія документа. Репозиторій оновлює документ лише тоді, коли версія в БД
    /// збігається з очікуваною, і збільшує її на 1 (див. <see cref="BumpVersion"/>).
    /// </summary>
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    public void BumpVersion() => Version++;
}
