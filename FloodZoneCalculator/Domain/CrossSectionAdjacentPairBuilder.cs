using System;
using System.Collections.Generic;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionAdjacentPairBuilder
{
    public IReadOnlyList<AdjacentCrossSectionPair> Build(
        CrossSectionSequence sequence)
    {
        if (sequence == null)
            throw new ArgumentNullException(nameof(sequence));

        var pairs = new List<AdjacentCrossSectionPair>(
            Math.Max(0, sequence.Items.Count - 1));

        for (var index = 1; index < sequence.Items.Count; index++)
        {
            pairs.Add(new AdjacentCrossSectionPair(
                sequence.Items[index - 1],
                sequence.Items[index]));
        }

        return Array.AsReadOnly(pairs.ToArray());
    }
}
