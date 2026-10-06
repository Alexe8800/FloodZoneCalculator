using Xunit;
using FloodZoneDb.Client;

namespace FloodZoneDb.Tests;

public sealed class OfflineModeTests
{
    [Fact]
    public void Connection_WithIsOfflineTrue_IsOfflineReturnsTrue()
    {
        var conn = new FloodZoneConnection("host=localhost", isOffline: true);
        Assert.True(conn.IsOffline);
    }

    [Fact]
    public void Connection_WithNullConnectionString_IsOfflineReturnsTrue()
    {
        var conn = new FloodZoneConnection(null);
        Assert.True(conn.IsOffline);
    }

    [Fact]
    public void Connection_WithEmptyConnectionString_IsOfflineReturnsFalse()
    {
        var conn = new FloodZoneConnection("");
        Assert.False(conn.IsOffline);
    }

    [Fact]
    public void Connection_WithValidConnectionString_IsOfflineReturnsFalse()
    {
        var conn = new FloodZoneConnection("host=localhost");
        Assert.False(conn.IsOffline);
    }

    [Fact]
    public async Task LoadObjectsAsync_ThrowsInOfflineMode()
    {
        var conn = new FloodZoneConnection("host=localhost", isOffline: true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await conn.LoadObjectsAsync());
        Assert.Contains("недоступна в оффлайн-режиме", ex.Message);
    }

    [Fact]
    public async Task CreateObjectAsync_ThrowsInOfflineMode()
    {
        var conn = new FloodZoneConnection("host=localhost", isOffline: true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await conn.CreateObjectAsync("hydro_node", "Test"));
        Assert.Contains("недоступна в оффлайн-режиме", ex.Message);
    }

    [Fact]
    public async Task CanConnectAsync_ReturnsFalseInOfflineMode()
    {
        var conn = new FloodZoneConnection("host=localhost", isOffline: true);
        var result = await conn.CanConnectAsync();
        Assert.False(result);
    }
}
