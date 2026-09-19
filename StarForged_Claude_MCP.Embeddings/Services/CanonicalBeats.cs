using StarForged_Claude_MCP.Embeddings.Database.Models;

namespace StarForged_Claude_MCP.Embeddings.Services;

/// <summary>
/// Picks the canonical reading of a session out of everything written for it.
///
/// A session is read back in the order it was written, except that a numbered beat keeps the
/// place of its *earliest* version while showing the content of its *latest*. So a beat 2
/// corrected long after beat 5 was written still reads between beats 1 and 3, rather than at
/// the end. Beats with no number of their own — vignettes, interludes — simply hold their own
/// place in write order.
/// </summary>
public static class CanonicalBeats
{
    private record BeatVersions(Beat Anchor, Beat Canonical);

    public static List<Beat> Select(IEnumerable<Beat> beatsInWriteOrder)
    {
        var beats = beatsInWriteOrder.ToList();

        var versionsByBeatNumber = beats
            .Where(b => b.BeatNumber != null)
            .GroupBy(b => b.BeatNumber!.Value)
            .ToDictionary(
                g => g.Key,
                g => new BeatVersions(
                    Anchor: g.OrderBy(b => b.Version).ThenBy(b => b.Id).First(),
                    Canonical: g.OrderByDescending(b => b.Version).ThenByDescending(b => b.Id).First()));

        var canonical = new List<Beat>();

        foreach (var beat in beats)
        {
            if (beat.BeatNumber == null)
            {
                canonical.Add(beat);
                continue;
            }

            var versions = versionsByBeatNumber[beat.BeatNumber.Value];

            if (beat.Id != versions.Anchor.Id) continue;

            canonical.Add(versions.Canonical);
        }

        int sequence = 1;
        foreach (var beat in canonical)
        {
            beat.Sequence = sequence++;
        }

        return canonical;
    }
}
