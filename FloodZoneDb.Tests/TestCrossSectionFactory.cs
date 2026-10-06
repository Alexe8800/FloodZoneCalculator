using FloodZoneDb.Client;
using System.Globalization;

namespace FloodZoneDb.Tests;

internal static class TestCrossSectionFactory
{
    public static CrossSectionRecord Symmetric() =>
        CreateSection(
            "TEST_PROFILE_01_SYMMETRIC",
            [
                Point("Left", "Bank", 1, 0, 110),
                Point("Left", "Horizontal", 2, 20, 108),
                Point("Left", "ChannelBank", 3, 40, 105),
                Point("Left", "Bottom", 4, 60, 100),
                Point("Center", "Bottom", 5, 80, 98),
                Point("Right", "Bottom", 6, 100, 100),
                Point("Right", "ChannelBank", 7, 120, 105),
                Point("Right", "Horizontal", 8, 140, 108),
                Point("Right", "Bank", 9, 160, 110)
            ]);

    public static CrossSectionRecord Asymmetric() =>
        CreateSection(
            "TEST_PROFILE_02_ASYMMETRIC",
            [
                Point("Left", "Bank", 1, 0, 112),
                Point("Left", "Horizontal", 2, 15, 109),
                Point("Left", "ChannelBank", 3, 35, 104),
                Point("Left", "Bottom", 4, 55, 101),
                Point("Center", "Bottom", 5, 75, 99),
                Point("Right", "Bottom", 6, 95, 100),
                Point("Right", "ChannelBank", 7, 118, 104),
                Point("Right", "Horizontal", 8, 145, 107),
                Point("Right", "Bank", 9, 175, 111)
            ]);

    public static CrossSectionRecord Incomplete() =>
        CreateSection(
            "TEST_PROFILE_03_INCOMPLETE",
            [
                Point("Left", "Bank", 1, 0, 110),
                Point("Left", "Horizontal", 2, 30, 106),
                Point("Center", "Bottom", 3, 70, 98),
                Point("Right", "Horizontal", 4, 130, 106),
                Point("Right", "Bank", 5, 160, 110)
            ]);

    public static CrossSectionRecord FlatBottom() =>
        CreateSection(
            "TEST_PROFILE_04_FLAT_BOTTOM",
            [
                Point("Left", "Bank", 1, 0, 110),
                Point("Left", "Horizontal", 2, 20, 107),
                Point("Left", "ChannelBank", 3, 40, 103),
                Point("Left", "Bottom", 4, 60, 100),
                Point("Center", "Bottom", 5, 80, 100),
                Point("Right", "Bottom", 6, 100, 100),
                Point("Right", "ChannelBank", 7, 120, 103),
                Point("Right", "Horizontal", 8, 140, 107),
                Point("Right", "Bank", 9, 160, 110)
            ]);

    public static CrossSectionRecord SlopedBottom() =>
        CreateSection(
            "TEST_PROFILE_05_SLOPED_BOTTOM",
            [
                Point("Left", "Bank", 1, 0, 112),
                Point("Left", "Horizontal", 2, 25, 108),
                Point("Left", "ChannelBank", 3, 45, 103),
                Point("Left", "Bottom", 4, 65, 100),
                Point("Center", "Bottom", 5, 85, 98),
                Point("Right", "Bottom", 6, 105, 99),
                Point("Right", "ChannelBank", 7, 125, 104),
                Point("Right", "Horizontal", 8, 150, 108),
                Point("Right", "Bank", 9, 180, 113)
            ]);

    private static CrossSectionRecord CreateSection(
        string name,
        List<BankPointRecord> points) =>
        new()
        {
            Number = 1,
            BankPoints = points
        };

    private static BankPointRecord Point(
        string side,
        string type,
        int number,
        double distance,
        double elevation) =>
        new()
        {
            Side = side,
            PointType = type,
            PointNumber = number,
            DistanceM = distance.ToString(CultureInfo.InvariantCulture),
            ElevationM = elevation.ToString(CultureInfo.InvariantCulture)
        };
}
