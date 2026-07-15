using Npgsql;

namespace BasvuruAkis.Api;

public static class PostgresConnectionStrings
{
    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            !IsPostgresUri(uri))
        {
            return connectionString;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        if (userInfo.Length > 0 && userInfo[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        return builder.ConnectionString;
    }

    private static bool IsPostgresUri(Uri uri)
    {
        return uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase);
    }
}
