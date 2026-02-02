using System;
using System.Globalization;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Serialization.State;
using EpochSim.Samples.Population;

namespace EpochSim.Execution.Validation
{
    public static class MinReproWriter
    {
        public static string Create(
            RunPaths paths,
            long failureTick,
            ulong seed,
            long endTick,
            string invariantName,
            string invariantMessage,
            JsonStateSerializer<WorldState> serializer,
            Func<WorldState> newState)
        {
            var minRoot = Path.Combine(paths.RunDir, "minrepro");
            Directory.CreateDirectory(minRoot);

            var dir = Path.Combine(minRoot, $"tick-{failureTick}");
            Directory.CreateDirectory(dir);

            var snapshotTick = CopyBestSnapshot(paths.SnapshotsDir, failureTick, Path.Combine(dir, "snapshot.json"));
            CopyEventsUpTo(paths.EventsPath, Path.Combine(dir, "events.jsonl"), snapshotTick, failureTick);

            var metaPath = Path.Combine(dir, "meta.txt");
            File.WriteAllLines(metaPath, new[]
            {
                $"failureTick={failureTick}",
                $"snapshotTick={snapshotTick}",
                $"seed={seed}",
                $"endTick={endTick}",
                $"invariantName={invariantName}",
                $"invariantMessage={invariantMessage}",
                $"createdUtc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}"
            });

            return dir;
        }

        private static long CopyBestSnapshot(string snapshotsDir, long failureTick, string dest)
        {
            if (!Directory.Exists(snapshotsDir))
                throw new DirectoryNotFoundException($"snapshots dir not found: {snapshotsDir}");

            var candidates = Directory.EnumerateFiles(snapshotsDir, "*.json")
                .Select(p => new FileInfo(p))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
                throw new FileNotFoundException($"no snapshots found in: {snapshotsDir}");

            FileInfo? best = null;
            long bestTick = 0;

            foreach (var f in candidates)
            {
                var t = TryParseTickFromSnapshotFileName(f.Name);
                if (t.HasValue && t.Value <= failureTick && t.Value >= bestTick)
                {
                    best = f;
                    bestTick = t.Value;
                }
            }

            if (best is null)
            {
                best = candidates[0];
                bestTick = TryParseTickFromSnapshotFileName(best.Name) ?? 0;
            }

            File.Copy(best.FullName, dest, overwrite: true);
            return bestTick;
        }

        private static long? TryParseTickFromSnapshotFileName(string name)
        {
            var digits = new string(name.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            if (long.TryParse(digits, out var t)) return t;
            return null;
        }

        private static void CopyEventsUpTo(string srcEvents, string destEvents, long snapshotTick, long failureTick)
        {
            if (!File.Exists(srcEvents))
                throw new FileNotFoundException($"events.jsonl not found: {srcEvents}");

            using var w = new StreamWriter(destEvents, append: false);

            foreach (var line in File.ReadLines(srcEvents))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var t = ReadLongField(line, "\"t\":");
                if (t <= snapshotTick) continue;
                if (t > failureTick) break;

                w.WriteLine(line);
            }
        }

        private static long ReadLongField(string line, string field)
        {
            var i = line.IndexOf(field, StringComparison.Ordinal);
            if (i < 0) throw new FormatException($"Missing field {field}");

            i += field.Length;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

            var end = i;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

            if (!long.TryParse(line.AsSpan(i, end - i), out var v))
                throw new FormatException($"Invalid number for {field}");

            return v;
        }
    }
}
