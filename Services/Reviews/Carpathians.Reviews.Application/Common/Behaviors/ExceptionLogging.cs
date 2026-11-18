namespace Carpathians.Reviews.Application.Common.Behaviors;

/// <summary>
/// Позначка «виняток уже залоговано» (у Exception.Data), щоб глобальний обробник WebApi
/// не логував той самий виняток вдруге — кожен виняток потрапляє в лог рівно один раз.
/// </summary>
public static class ExceptionLogging
{
    public const string LoggedKey = "Carpathians.Logged";

    public static void MarkLogged(this Exception ex) => ex.Data[LoggedKey] = true;

    public static bool IsLogged(this Exception ex) => ex.Data.Contains(LoggedKey);
}
