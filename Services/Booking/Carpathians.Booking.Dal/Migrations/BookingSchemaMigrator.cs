using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Migrations;

/// <summary>
/// Застосовує SQL-міграції Бронювання (вбудовані ресурси Migrations/*.sql) у порядку номерів.
/// Скрипти ідемпотентні (IF OBJECT_ID / COL_LENGTH), тож запуск під час кожного старту безпечний.
/// Пакети розділяються рядком GO, як у sqlcmd.
/// </summary>
public static partial class BookingSchemaMigrator
{
    public static async Task<IReadOnlyList<string>> ApplyAsync(string connectionString, CancellationToken ct = default)
    {
        var assembly = typeof(BookingSchemaMigrator).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var script in scripts)
        {
            foreach (var batch in SplitBatches(await ReadAsync(assembly, script)))
                await connection.ExecuteAsync(new CommandDefinition(batch, cancellationToken: ct));
        }

        return scripts;
    }

    private static async Task<string> ReadAsync(Assembly assembly, string resource)
    {
        await using var stream = assembly.GetManifestResourceStream(resource)
                                 ?? throw new InvalidOperationException($"Migration {resource} not found");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static IEnumerable<string> SplitBatches(string sql)
        => GoSeparator().Split(sql).Select(b => b.Trim()).Where(b => b.Length > 0);

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
