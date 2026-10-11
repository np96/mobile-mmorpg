#if UNITY_EDITOR || DEVELOPMENT_BUILD || SESSION_RPG_CAPTURE
using System;
using System.Globalization;
using System.IO;
using SessionRpg.Core;

namespace SessionRpg.Unity.Performance
{
    /// <summary>Measurement availability is separate from a successful zero-cost sample.</summary>
    public static class ProfileCaptureMeasurementContract
    {
        public const long UnavailableAllocationBytes = -1L;
        public const string Il2CppAllocationUnavailableReason =
            "Unavailable: this Unity IL2CPP runtime does not implement GC.GetAllocatedBytesForCurrentThread; the native call was not invoked.";

        public static string DescribeThreadAllocationCounter(bool il2CppBackend)
        {
            return il2CppBackend ? Il2CppAllocationUnavailableReason :
                "GC.GetAllocatedBytesForCurrentThread; current observer thread only. Negative samples mean unavailable, not zero allocation.";
        }

        public static string DescribeLimitations(string runtimePlatform)
        {
            return "Observed build/platform only (" + runtimePlatform + "); not cross-device, sustained thermal, device-touch, worst-case casting, ten-session soak, or release acceptance. " +
                "Inter-frame and Main Thread timing include waits/overhead. Supported main-thread managed allocation samples include the diagnostic observer and exclude other threads. " +
                "Unsupported allocation counters are not called and report -1. Unity memory counters are not process RSS; final serialization is outside measurement. " +
                "Missing counters/timings are -1, never zero. Existing local session records retain normal behavior; PvE XP uses the unique diagnostic save directory.";
        }
    }

    /// <summary>Strict, bounded, opt-in settings for the diagnostic player only.</summary>
    public sealed class SessionRpgProfileCaptureOptions
    {
        public const string OutputArgument = "-sessionRpgProfile";
        public const int MaximumSamples = 1800;
        public const double SampleDeadlineSeconds = 60d;
        public string OutputPath { get; private set; }
        public string Specialization { get; private set; } = "Discipline";
        public int Camp { get; private set; } = 1;
        public int Samples { get; private set; } = 600;
        public double WarmupSeconds { get; private set; } = 10d;

        public static bool TryParse(string[] arguments,
            out SessionRpgProfileCaptureOptions options, out string error)
        {
            options = null;
            error = string.Empty;
            if (arguments == null || Array.IndexOf(arguments, OutputArgument) < 0)
            {
                return false;
            }
            if (Array.IndexOf(arguments, "-sessionRpgCapture") >= 0)
            {
                error = "Live profiling and frozen reference capture must run separately.";
                return false;
            }
            var candidate = new SessionRpgProfileCaptureOptions();
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                if (!argument.StartsWith("-sessionRpgProfile", StringComparison.Ordinal))
                {
                    continue;
                }
                if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    error = "Missing diagnostic argument value: " + argument;
                    return false;
                }
                string value = arguments[++index];
                switch (argument)
                {
                    case OutputArgument:
                        if (candidate.OutputPath != null || !IsAllowedOutputPath(value))
                        {
                            error = "Profile output must be one absolute .json path outside Assets.";
                            return false;
                        }
                        candidate.OutputPath = Path.GetFullPath(value);
                        break;
                    case "-sessionRpgProfileCamp":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int camp) || camp < 0 || camp > 6)
                        {
                            error = "Profile camp must be 0..6.";
                            return false;
                        }
                        candidate.Camp = camp;
                        break;
                    case "-sessionRpgProfileSpecialization":
                        if (!Enum.TryParse(value, true, out SpecializationId specialization) ||
                            specialization == SpecializationId.None || specialization == SpecializationId.Boss ||
                            !Enum.IsDefined(typeof(SpecializationId), specialization) ||
                            !string.Equals(value, specialization.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            error = "Profile specialization must name a playable specialization.";
                            return false;
                        }
                        candidate.Specialization = specialization.ToString();
                        break;
                    case "-sessionRpgProfileSamples":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int samples) || samples < 120 || samples > MaximumSamples)
                        {
                            error = "Profile sample count must be 120..1800.";
                            return false;
                        }
                        candidate.Samples = samples;
                        break;
                    case "-sessionRpgProfileWarmupSeconds":
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double warmup) ||
                            double.IsNaN(warmup) || double.IsInfinity(warmup) || warmup < 5d || warmup > 30d)
                        {
                            error = "Profile warmup must be finite and within 5..30 seconds.";
                            return false;
                        }
                        candidate.WarmupSeconds = warmup;
                        break;
                    default:
                        error = "Unknown diagnostic argument: " + argument;
                        return false;
                }
            }
            options = candidate;
            return true;
        }

        public static bool IsAllowedOutputPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) ||
                    !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                string fullPath = Path.GetFullPath(path);
                string[] segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (string segment in segments)
                {
                    if (string.Equals(segment, "Assets", StringComparison.OrdinalIgnoreCase)) { return false; }
                }
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
            {
                return false;
            }
        }
    }

    [Serializable]
    public sealed class ProfileMetricSummary
    {
        public int validSamples;
        public double minimum = -1d;
        public double median = -1d;
        public double p95 = -1d;
        public double maximum = -1d;
        public double mean = -1d;

        /// <summary>Nearest-rank percentiles; negative/non-finite values mean unavailable, not zero.</summary>
        public static ProfileMetricSummary From(double[] values, int count)
        {
            if (values == null || count < 0 || count > values.Length) { throw new ArgumentOutOfRangeException(nameof(count)); }
            var result = new ProfileMetricSummary();
            var sorted = new double[count];
            double total = 0d;
            for (int index = 0; index < count; index++)
            {
                double value = values[index];
                if (value < 0d || double.IsNaN(value) || double.IsInfinity(value)) { continue; }
                sorted[result.validSamples++] = value;
                total += value;
            }
            if (result.validSamples == 0) { return result; }
            Array.Sort(sorted, 0, result.validSamples);
            result.minimum = sorted[0];
            result.median = sorted[(int)Math.Ceiling(result.validSamples * 0.5d) - 1];
            result.p95 = sorted[(int)Math.Ceiling(result.validSamples * 0.95d) - 1];
            result.maximum = sorted[result.validSamples - 1];
            result.mean = total / result.validSamples;
            return result;
        }
    }

    /// <summary>Describes recorded emissions without correcting or discarding raw values.</summary>
    [Serializable]
    public sealed class ProfileCounterEmissionSummary
    {
        public int observedFrames;
        public int unavailableFrames;
        public int zeroEmissionFrames;
        public int multipleEmissionFrames;
        public int zeroValueFrames;
        public long totalEmissions;

        public static ProfileCounterEmissionSummary From(double[] values, long[] emissions, int count)
        {
            if (values == null || emissions == null || count < 0 || count > values.Length || count > emissions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            var result = new ProfileCounterEmissionSummary();
            for (int index = 0; index < count; index++)
            {
                if (emissions[index] < 0 || values[index] < 0d || double.IsNaN(values[index]) || double.IsInfinity(values[index]))
                {
                    result.unavailableFrames++;
                    continue;
                }
                result.observedFrames++;
                result.totalEmissions += emissions[index];
                if (emissions[index] == 0) { result.zeroEmissionFrames++; }
                if (emissions[index] > 1) { result.multipleEmissionFrames++; }
                if (values[index] == 0d) { result.zeroValueFrames++; }
            }
            return result;
        }
    }
}
#endif
