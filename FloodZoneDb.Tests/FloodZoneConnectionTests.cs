using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class FloodZoneConnectionTests
{
    [Fact]
    public async Task Connection_to_database_is_available()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connection = new FloodZoneConnection(connectionString);
        Assert.True(await connection.CanConnectAsync());
    }

    [Fact]
    public void Missing_connection_string_is_reported()
    {
        var previous = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
        try
        {
            Environment.SetEnvironmentVariable("FLOODZONE_CONN", null);
            var exception = Assert.Throws<InvalidOperationException>(
                () => FloodZoneConnection.FromEnvironment());
            Assert.Contains("FLOODZONE_CONN", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOODZONE_CONN", previous);
        }
    }
}
