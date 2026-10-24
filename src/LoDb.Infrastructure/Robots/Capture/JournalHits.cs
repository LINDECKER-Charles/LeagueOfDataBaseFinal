using System.Net;
using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Persistence.Robots;
using LoDb.Infrastructure.Persistence.Robots.Writing;
using LoDb.Infrastructure.Robots.Detection;
using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Verification;

namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>
/// The hits of a batch of records: a request whose user agent names a robot is one; a page
/// request of a browser's user agent counts towards its client's pace and is one only past
/// the pace of a reader. Then the network of each address, and the verdict on each robot.
/// </summary>
/// <remarks>The other requests of browsers are dropped here: nothing of them is kept.</remarks>
internal sealed class JournalHits(
    UndeclaredDetector detector,
    NetworkResolver resolver,
    VisitorHasher hasher)
{
    public async Task<IReadOnlyList<RobotHitRecord>> BuildAsync(
        IReadOnlyList<JournalRecord> records,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = Classify(records);
        var addresses = candidates
            .Select(static candidate => candidate.Record.Address)
            .OfType<IPAddress>()
            .ToList();
        var lookups = await resolver.ResolveAsync(addresses, cancellationToken);
        return
        [
            .. candidates.Select(candidate =>
                HitOf(candidate, candidate.Record.Address is { } address
                    ? lookups.GetValueOrDefault(address)
                    : ReverseLookup.NoName)),
        ];
    }

    private List<Candidate> Classify(IReadOnlyList<JournalRecord> records)
    {
        var candidates = new List<Candidate>();
        foreach (var record in records)
        {
            var section = HitSections.Of(record.Target);
            if (RobotCatalog.Identify(record.Agent) is { } profile)
            {
                candidates.Add(new Candidate(record, section, profile));
            }
            else if (HitSections.IsPage(section) && IsPastPace(record))
            {
                candidates.Add(new Candidate(record, section, Profile: null));
            }
        }

        return candidates;
    }

    private bool IsPastPace(JournalRecord record)
    {
        var client = hasher.Hash(record.Address?.ToString(), record.Agent);
        detector.Count(client, record.At);
        return detector.IsRobot(client, record.At);
    }

    private static RobotHitRecord HitOf(Candidate candidate, ReverseLookup? lookup)
    {
        var record = candidate.Record;
        var network = RobotVerifier.NetworkOf(lookup);
        return new RobotHitRecord
        {
            OccurredAt = record.At,
            Agent = TextCut.To(record.Agent?.Trim() ?? string.Empty, RobotAgent.AgentMaxLength),
            Detection = candidate.Profile is null ? RobotDetections.Rate : RobotDetections.Agent,
            Verdict = candidate.Profile is { } profile
                ? RobotVerifier.VerdictOf(profile, lookup)
                : RobotVerdicts.Unverifiable,
            Network = network is null ? null : TextCut.To(network, RobotHit.NetworkMaxLength),
            Method = TextCut.To(record.Method, RobotHit.MethodMaxLength),
            Target = TextCut.To(record.Target, RobotHit.TargetMaxLength),
            Section = candidate.Section,
            Status = record.Status,
            DurationMs = record.DurationMs,
            Bytes = record.Bytes,
            Cache = record.Cache is null ? null : TextCut.To(record.Cache, RobotHit.LabelMaxLength),
        };
    }

    // A robot by its user agent carries its profile; an undeclared one none.
    private readonly record struct Candidate(
        JournalRecord Record,
        string Section,
        RobotProfile? Profile);
}
