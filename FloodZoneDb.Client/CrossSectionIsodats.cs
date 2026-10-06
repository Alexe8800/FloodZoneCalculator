using System.Collections.ObjectModel;

namespace FloodZoneDb.Client;

public sealed class CrossSectionIsodats
{
    public int CrossSectionNumber { get; }
    public IsodatSideIsodats? Left { get; }
    public IsodatSideIsodats? Right { get; }

    internal CrossSectionIsodats(
        int crossSectionNumber,
        IsodatSideIsodats? left,
        IsodatSideIsodats? right)
    {
        CrossSectionNumber = crossSectionNumber;
        Left = left;
        Right = right;
    }
}

public sealed class IsodatSideIsodats
{
    public IsodatSide Side { get; }
    public IReadOnlyDictionary<IsodatType, IReadOnlyList<IsodatRecord>> ByType { get; }

    internal IsodatSideIsodats(
        IsodatSide side,
        IDictionary<IsodatType, List<IsodatRecord>> recordsByType)
    {
        Side = side;
        var readOnlyRecords = recordsByType.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<IsodatRecord>)Array.AsReadOnly(pair.Value.ToArray()));
        ByType = new ReadOnlyDictionary<IsodatType, IReadOnlyList<IsodatRecord>>(readOnlyRecords);
    }
}

public sealed class IsodatGrouping
{
    public IReadOnlyList<CrossSectionIsodats> CrossSections { get; }

    internal IsodatGrouping(IEnumerable<CrossSectionIsodats> crossSections)
    {
        CrossSections = Array.AsReadOnly(crossSections.ToArray());
    }

    public IsodatSideIsodats? GetSet(int crossSectionNumber, IsodatSide side)
    {
        var section = CrossSections.FirstOrDefault(
            item => item.CrossSectionNumber == crossSectionNumber);
        if (section == null)
            return null;

        return side switch
        {
            IsodatSide.Left => section.Left,
            IsodatSide.Right => section.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Неизвестная сторона створа.")
        };
    }
}

public static class IsodatGrouper
{
    public static IsodatGrouping GroupByCrossSection(IReadOnlyList<IsodatRecord> records)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        var sections = new SortedDictionary<int, Dictionary<IsodatSide, Dictionary<IsodatType, List<IsodatRecord>>>>();
        foreach (var record in records)
        {
            if (record == null)
                throw new ArgumentException("Коллекция содержит пустую запись изодаты.", nameof(records));
            if (record.CrossSectionNumber <= 0)
                throw new ArgumentException("Номер створа должен быть положительным.", nameof(records));
            if (!Enum.IsDefined(typeof(IsodatSide), record.Side))
                throw new ArgumentException("Запись содержит неизвестную сторону.", nameof(records));
            if (!Enum.IsDefined(typeof(IsodatType), record.IsodatType))
                throw new ArgumentException("Запись содержит неизвестный тип изодаты.", nameof(records));

            if (!sections.TryGetValue(record.CrossSectionNumber, out var sides))
            {
                sides = new Dictionary<IsodatSide, Dictionary<IsodatType, List<IsodatRecord>>>();
                sections.Add(record.CrossSectionNumber, sides);
            }

            if (!sides.TryGetValue(record.Side, out var types))
            {
                types = new Dictionary<IsodatType, List<IsodatRecord>>();
                sides.Add(record.Side, types);
            }

            if (!types.TryGetValue(record.IsodatType, out var typeRecords))
            {
                typeRecords = new List<IsodatRecord>();
                types.Add(record.IsodatType, typeRecords);
            }

            typeRecords.Add(record);
        }

        return new IsodatGrouping(sections.Select(section =>
        {
            section.Value.TryGetValue(IsodatSide.Left, out var leftTypes);
            section.Value.TryGetValue(IsodatSide.Right, out var rightTypes);
            return new CrossSectionIsodats(
                section.Key,
                leftTypes == null ? null : new IsodatSideIsodats(IsodatSide.Left, leftTypes),
                rightTypes == null ? null : new IsodatSideIsodats(IsodatSide.Right, rightTypes));
        }));
    }
}
