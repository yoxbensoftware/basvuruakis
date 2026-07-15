using BasvuruAkis.Api;
using BasvuruAkis.Api.Services;
using Npgsql;

namespace BasvuruAkis.Api.Tests;

public sealed class DomainSecurityTests
{
    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000145", false)]
    [InlineData("01234567890", false)]
    [InlineData("abc", false)]
    public void TcknValidator_ValidatesAlgorithmicRules(string value, bool expected)
    {
        var validator = new TcknValidator();

        var actual = validator.IsValid(value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CryptoService_EncryptsAndHashesSensitiveValues()
    {
        var provider = new StaticKeyProvider();
        var crypto = new CryptoService(provider);

        var encrypted = crypto.Encrypt("10000000146");
        var decrypted = crypto.Decrypt(encrypted);
        var firstHash = crypto.HashLookup("10000000146");
        var secondHash = crypto.HashLookup("10000000146");

        Assert.NotEqual("10000000146", encrypted);
        Assert.Equal("10000000146", decrypted);
        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Theory]
    [InlineData("=cmd|'/C calc'!A0")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-10+20")]
    [InlineData("@malicious")]
    [InlineData(" \t=HYPERLINK(\"http://example.test\")")]
    public void ExportSanitizer_PrefixesFormulaInjectionPayloads(string payload)
    {
        var sanitizer = new ExportSanitizer();

        var actual = sanitizer.SanitizeCell(payload);

        Assert.StartsWith("'", actual);
    }

    [Fact]
    public void PostgresConnectionStrings_NormalizesRenderDatabaseUrl()
    {
        var normalized = PostgresConnectionStrings.Normalize(
            "postgresql://user%2Dname:p%40ss%3Aword@dpg-test-a:5432/basvuruakis");

        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("dpg-test-a", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("basvuruakis", builder.Database);
        Assert.Equal("user-name", builder.Username);
        Assert.Equal("p@ss:word", builder.Password);
    }

    [Fact]
    public void PostgresConnectionStrings_LeavesNpgsqlConnectionStringUnchanged()
    {
        const string connectionString = "Host=localhost;Port=5432;Database=basvuruakis;Username=test;Password=secret";

        var normalized = PostgresConnectionStrings.Normalize(connectionString);

        Assert.Equal(connectionString, normalized);
    }

    private sealed class StaticKeyProvider : IDataProtectionKeyProvider
    {
        public byte[] EncryptionKey { get; } = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        public byte[] LookupKey { get; } = Enumerable.Range(32, 32).Select(x => (byte)x).ToArray();
    }
}
