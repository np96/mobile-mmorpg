using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Unity.Performance;

namespace SessionRpg.Tests.Editor
{
    public sealed class SessionRpgProfileCaptureTests
    {
        private static string OutputPath => Path.Combine(Path.GetTempPath(), "session-rpg-profile-contract.json");

        [Test]
        public void NormalLaunch_IsInertWithoutTheOptInArgument()
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] { "player", "-screen-width", "1920" },
                out var options, out string error), Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void OptIn_DefaultsToBoundedLiveFrostboundObservation()
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] { "-sessionRpgProfile", OutputPath },
                out var options, out string error), Is.True, error);
            Assert.That(options.Camp, Is.EqualTo(1));
            Assert.That(options.Specialization, Is.EqualTo("Discipline"));
            Assert.That(options.Samples, Is.EqualTo(600));
            Assert.That(options.WarmupSeconds, Is.EqualTo(10d));
            Assert.That(SessionRpgProfileCaptureOptions.SampleDeadlineSeconds, Is.EqualTo(60d));
        }

        [Test]
        public void ExplicitSettings_RetainStableCampAndSpecializationIdentities()
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] {
                "-sessionRpgProfile", OutputPath, "-sessionRpgProfileCamp", "6",
                "-sessionRpgProfileSpecialization", "ShadowDance", "-sessionRpgProfileSamples", "120",
                "-sessionRpgProfileWarmupSeconds", "5.5" }, out var options, out string error), Is.True, error);
            Assert.That(options.Camp, Is.EqualTo(6));
            Assert.That(options.Specialization, Is.EqualTo("ShadowDance"));
            Assert.That(options.Samples, Is.EqualTo(120));
            Assert.That(options.WarmupSeconds, Is.EqualTo(5.5d));
        }

        [TestCase("-sessionRpgProfileCamp", "7")]
        [TestCase("-sessionRpgProfileCamp", "-1")]
        [TestCase("-sessionRpgProfileSamples", "119")]
        [TestCase("-sessionRpgProfileSamples", "1801")]
        [TestCase("-sessionRpgProfileWarmupSeconds", "NaN")]
        [TestCase("-sessionRpgProfileWarmupSeconds", "Infinity")]
        [TestCase("-sessionRpgProfileWarmupSeconds", "4.9")]
        [TestCase("-sessionRpgProfileWarmupSeconds", "30.1")]
        [TestCase("-sessionRpgProfileSpecialization", "Boss")]
        [TestCase("-sessionRpgProfileSpecialization", "None")]
        [TestCase("-sessionRpgProfileSpecialization", "1")]
        [TestCase("-sessionRpgProfileUnknown", "1")]
        public void InvalidOrUnboundedOptions_AreRejected(string argument, string value)
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] { "-sessionRpgProfile", OutputPath, argument, value },
                out var options, out string error), Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void MissingAndDuplicateOutputArguments_AreRejected()
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] { "-sessionRpgProfile" }, out _, out _), Is.False);
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] {
                "-sessionRpgProfile", OutputPath, "-sessionRpgProfile", OutputPath }, out _, out _), Is.False);
        }

        [Test]
        public void FrozenScreenshotAndLiveProfile_CannotRunTogether()
        {
            Assert.That(SessionRpgProfileCaptureOptions.TryParse(new[] {
                "-sessionRpgProfile", OutputPath, "-sessionRpgCapture", Path.ChangeExtension(OutputPath, ".png") },
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("separately"));
        }

        [Test]
        public void Output_IsAbsoluteJsonOutsideUnityAssets()
        {
            Assert.That(SessionRpgProfileCaptureOptions.IsAllowedOutputPath(OutputPath), Is.True);
            Assert.That(SessionRpgProfileCaptureOptions.IsAllowedOutputPath("profile.json"), Is.False);
            Assert.That(SessionRpgProfileCaptureOptions.IsAllowedOutputPath(Path.ChangeExtension(OutputPath, ".png")), Is.False);
            Assert.That(SessionRpgProfileCaptureOptions.IsAllowedOutputPath(Path.Combine(Path.GetTempPath(), "Assets", "profile.json")), Is.False);
            Assert.That(SessionRpgProfileCaptureOptions.IsAllowedOutputPath(Path.Combine(Path.GetTempPath(), "assets", "profile.json")), Is.False);
        }

        [Test]
        public void Summary_UsesNearestRankPercentilesWithoutMutatingEvidence()
        {
            var values = new double[100];
            for (int index = 0; index < values.Length; index++) { values[index] = 100 - index; }
            ProfileMetricSummary result = ProfileMetricSummary.From(values, values.Length);
            Assert.That(result.validSamples, Is.EqualTo(100));
            Assert.That(result.minimum, Is.EqualTo(1));
            Assert.That(result.median, Is.EqualTo(50));
            Assert.That(result.p95, Is.EqualTo(95));
            Assert.That(result.maximum, Is.EqualTo(100));
            Assert.That(result.mean, Is.EqualTo(50.5d));
            Assert.That(values[0], Is.EqualTo(100));
        }

        [Test]
        public void MissingCounterValues_DoNotMasqueradeAsZero()
        {
            ProfileMetricSummary absent = ProfileMetricSummary.From(new[] { -1d, double.NaN, double.PositiveInfinity }, 3);
            Assert.That(absent.validSamples, Is.Zero);
            Assert.That(absent.minimum, Is.EqualTo(-1));
            Assert.That(absent.p95, Is.EqualTo(-1));
            ProfileMetricSummary zero = ProfileMetricSummary.From(new[] { -1d, 0d, 0d }, 3);
            Assert.That(zero.validSamples, Is.EqualTo(2));
            Assert.That(zero.p95, Is.Zero);
        }

        [Test]
        public void PartialWindow_DoesNotReadUnobservedTrailingZeros()
        {
            ProfileMetricSummary result = ProfileMetricSummary.From(new[] { 17d, 23d, 0d, 0d }, 2);
            Assert.That(result.validSamples, Is.EqualTo(2));
            Assert.That(result.minimum, Is.EqualTo(17d));
            Assert.That(result.p95, Is.EqualTo(23d));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileMetricSummary.From(new double[2], 3));
        }

        [Test]
        public void EmissionMetadata_DistinguishesMissingZeroAndAccumulatedFramesWithoutCorrectingValues()
        {
            var values = new[] { -1d, 0d, 40d, 80d, 0d };
            var emissions = new long[] { -1, 0, 1, 2, 1 };
            ProfileCounterEmissionSummary result = ProfileCounterEmissionSummary.From(values, emissions, 5);
            Assert.That(result.observedFrames, Is.EqualTo(4));
            Assert.That(result.unavailableFrames, Is.EqualTo(1));
            Assert.That(result.zeroEmissionFrames, Is.EqualTo(1));
            Assert.That(result.multipleEmissionFrames, Is.EqualTo(1));
            Assert.That(result.zeroValueFrames, Is.EqualTo(2));
            Assert.That(result.totalEmissions, Is.EqualTo(4));
            Assert.That(values[3], Is.EqualTo(80d), "Aggregation metadata must not divide or replace the raw value.");
            Assert.That(ProfileMetricSummary.From(values, 5).p95, Is.EqualTo(80d));
        }

        [Test]
        public void EmissionMetadata_ExcludesUnobservedCapacityAndRejectsMismatchedArrays()
        {
            ProfileCounterEmissionSummary result = ProfileCounterEmissionSummary.From(
                new[] { 40d, 0d, 0d }, new long[] { 1, 0, 0 }, 1);
            Assert.That(result.observedFrames, Is.EqualTo(1));
            Assert.That(result.zeroEmissionFrames, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileCounterEmissionSummary.From(new double[3], new long[2], 3));
        }

        [Test]
        public void Il2CppAllocationMetadata_ReportsUnsupportedRatherThanAZeroCostMeasurement()
        {
            Assert.That(ProfileCaptureMeasurementContract.UnavailableAllocationBytes, Is.EqualTo(-1L));
            string description = ProfileCaptureMeasurementContract.DescribeThreadAllocationCounter(true);
            Assert.That(description, Does.Contain("Unavailable"));
            Assert.That(description, Does.Contain("IL2CPP"));
            Assert.That(description, Does.Contain("not invoked"));
            var summary = ProfileMetricSummary.From(new[] {
                (double)ProfileCaptureMeasurementContract.UnavailableAllocationBytes,
                (double)ProfileCaptureMeasurementContract.UnavailableAllocationBytes }, 2);
            Assert.That(summary.validSamples, Is.Zero);
            Assert.That(summary.p95, Is.EqualTo(-1d));
        }

        [Test]
        public void MonoAllocationMetadata_RetainsTheObservedThreadScope()
        {
            string description = ProfileCaptureMeasurementContract.DescribeThreadAllocationCounter(false);
            Assert.That(description, Does.Contain("current observer thread only"));
            Assert.That(description, Does.Not.Contain("native call was not invoked"));
        }

        [TestCase("Android")]
        [TestCase("OSXPlayer")]
        [TestCase("WindowsPlayer")]
        public void Limitations_NameTheActualPlatformWithoutMislabelingAndroidEvidenceAsDesktop(string platform)
        {
            string description = ProfileCaptureMeasurementContract.DescribeLimitations(platform);
            Assert.That(description, Does.Contain("(" + platform + ")"));
            Assert.That(description, Does.Not.Contain("Desktop Development-player evidence only"));
            Assert.That(description, Does.Not.Contain("not Android"));
            Assert.That(description, Does.Contain("not cross-device"));
            Assert.That(description, Does.Contain("-1"));
        }
    }
}
