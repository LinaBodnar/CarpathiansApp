namespace Carpathians.Reviews.Application.Common.Models;

/// <summary>
/// Сторінка курсорної пагінації: наступна сторінка починається після <see cref="NextCursor"/>
/// (Id останнього отриманого документа), без skip.
/// </summary>
public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
