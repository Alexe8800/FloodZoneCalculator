using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionPointsIntegrationTests
{
    [Fact]
    public async Task Cross_section_points_round_trip_update_and_cleanup()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connection = FloodZoneConnection.FromEnvironment();
        await connection.EnsureDatabaseAndSchemaAsync();

        var typeCode = "integration_test_" + Guid.NewGuid().ToString("N");
        var objectName = "INTEGRATION_TEST_DO_NOT_USE_" + Guid.NewGuid().ToString("N");
        long objectId = 0;

        try
        {
            await connection.EnsureObjectTypeAsync(typeCode, "Integration test type");
            objectId = await connection.CreateObjectAsync(typeCode, objectName);

            var expected = CreatePoints();
            var section = new CrossSectionRecord
            {
                ObjectId = objectId,
                Number = 1,
                DistanceFromHydroUnitM = "100",
                BottomLevelZb = "65",
                DepthHb = "5",
                WidthBb = "107",
                VelocityVb = "0.5",
                Kgm = "1",
                BankPoints = expected
            };

            var sectionId = await connection.SaveCrossSectionAsync(section);
            var loaded = await LoadSingleSectionAsync(connection, objectId);
            Assert.Equal(sectionId, loaded.Id);
            AssertPointsEqual(expected, loaded.BankPoints);

            var updated = expected[4];
            updated.ElevationM = "66.75";
            await connection.SaveCrossSectionAsync(section);

            loaded = await LoadSingleSectionAsync(connection, objectId);
            Assert.Equal("66.75", loaded.BankPoints.Single(p =>
                p.Side == "Center" && p.PointType == "Bottom").ElevationM);
            AssertPointsEqual(expected, loaded.BankPoints);
        }
        finally
        {
            if (objectId > 0)
                await connection.DeleteObjectAsync(objectId);

            await connection.DeleteObjectTypeAsync(typeCode);
        }

        Assert.Empty(await connection.LoadCrossSectionsAsync(objectId));
        Assert.DoesNotContain(await connection.LoadObjectsAsync(), o => o.Id == objectId);
    }

    private static async Task<CrossSectionRecord> LoadSingleSectionAsync(
        FloodZoneConnection connection, long objectId)
    {
        var sections = await connection.LoadCrossSectionsAsync(objectId);
        return Assert.Single(sections);
    }

    private static void AssertPointsEqual(
        IReadOnlyList<BankPointRecord> expected,
        IReadOnlyList<BankPointRecord> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var expectedPoint in expected)
        {
            var actualPoint = Assert.Single(actual, point =>
                point.Side == expectedPoint.Side &&
                point.PointType == expectedPoint.PointType &&
                point.PointNumber == expectedPoint.PointNumber);

            Assert.Equal(expectedPoint.DistanceM, actualPoint.DistanceM);
            Assert.Equal(expectedPoint.ElevationM, actualPoint.ElevationM);
        }
    }

    private static List<BankPointRecord> CreatePoints() =>
    [
        new() { Side = "Left", PointType = "Bank", PointNumber = 1, DistanceM = "35", ElevationM = "65" },
        new() { Side = "Left", PointType = "Horizontal", PointNumber = 2, DistanceM = "55", ElevationM = "73" },
        new() { Side = "Left", PointType = "ChannelBank", PointNumber = 3, DistanceM = "65", ElevationM = "74" },
        new() { Side = "Left", PointType = "Bottom", PointNumber = 4, DistanceM = "69", ElevationM = "75" },
        new() { Side = "Center", PointType = "Bottom", PointNumber = 5, DistanceM = "0", ElevationM = "60" },
        new() { Side = "Right", PointType = "Bottom", PointNumber = 6, DistanceM = "72", ElevationM = "60" },
        new() { Side = "Right", PointType = "ChannelBank", PointNumber = 7, DistanceM = "74", ElevationM = "70.5" },
        new() { Side = "Right", PointType = "Horizontal", PointNumber = 8, DistanceM = "95", ElevationM = "71" },
        new() { Side = "Right", PointType = "Bank", PointNumber = 9, DistanceM = "105", ElevationM = "71.4" }
    ];
}
