namespace RespawnSwitch.Application.Douyin;

public static class DouyinCandidateSelector
{
    public static DouyinDiscoveryResult Select(IEnumerable<DouyinCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var distinct = candidates
            .GroupBy(candidate => candidate.NormalizedPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate, CandidateRankComparer.Instance).First())
            .OrderByDescending(candidate => candidate, CandidateRankComparer.Instance)
            .ThenBy(candidate => candidate.NormalizedPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length == 0)
        {
            return new(
                DouyinDiscoveryStatus.NotFound,
                null,
                distinct,
                DouyinScanProgress.Empty,
                "douyin.discovery.not-found");
        }

        var top = distinct[0];
        var tied = distinct.Skip(1).TakeWhile(candidate => CandidateRankComparer.Instance.Compare(top, candidate) == 0).Any();
        return tied
            ? new(
                DouyinDiscoveryStatus.Ambiguous,
                null,
                distinct,
                DouyinScanProgress.Empty,
                "douyin.discovery.ambiguous")
            : new(
                DouyinDiscoveryStatus.Found,
                top,
                distinct,
                DouyinScanProgress.Empty,
                "douyin.discovery.found");
    }

    private sealed class CandidateRankComparer : IComparer<DouyinCandidate>
    {
        public static CandidateRankComparer Instance { get; } = new();

        public int Compare(DouyinCandidate? left, DouyinCandidate? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var comparison = left.IsRunning.CompareTo(right.IsRunning);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = SourceRank(left.Source).CompareTo(SourceRank(right.Source));
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.HasTrustedSignature.CompareTo(right.HasTrustedSignature);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.FileVersion.CompareTo(right.FileVersion);
            return comparison != 0
                ? comparison
                : left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
        }

        private static int SourceRank(DouyinDiscoverySource source) => source switch
        {
            DouyinDiscoverySource.RunningProcess => 5,
            DouyinDiscoverySource.SavedPath => 4,
            DouyinDiscoverySource.Registry => 3,
            DouyinDiscoverySource.StartMenu => 2,
            DouyinDiscoverySource.FullDisk => 1,
            _ => 0
        };
    }
}
