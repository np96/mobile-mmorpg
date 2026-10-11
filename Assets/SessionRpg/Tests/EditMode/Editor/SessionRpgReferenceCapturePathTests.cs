using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Unity.Gameplay;

namespace SessionRpg.Tests.Editor
{
    public sealed class SessionRpgReferenceCapturePathTests
    {
        private static string Persistent => Path.Combine(Path.GetTempPath(), "session-rpg-capture-root");

        [Test]
        public void DesktopRetainsAbsoluteDestinationOutsidePersistentDirectory()
        {
            string output = Path.Combine(Path.GetTempPath(), "desktop-review", "frame.png");
            Assert.That(SessionRpgCapturePath.ScreenshotFileName(output, Persistent, false),
                Is.EqualTo(Path.GetFullPath(output)));
        }

        [Test]
        public void AndroidSubmitsRelativeNameButResolvesToTheExactPolledDestination()
        {
            string output = Path.Combine(Persistent, "review", "frame.png");
            string relative = SessionRpgCapturePath.ScreenshotFileName(output, Persistent, true);
            Assert.That(Path.IsPathRooted(relative), Is.False);
            Assert.That(relative, Is.EqualTo(Path.Combine("review", "frame.png")));
            Assert.That(Path.GetFullPath(Path.Combine(Persistent, relative)), Is.EqualTo(output));
        }

        [Test]
        public void AndroidCanonicalizesInternalParentSegments()
        {
            string output = Path.Combine(Persistent, "temporary", "..", "review", "frame.png");
            Assert.That(SessionRpgCapturePath.ScreenshotFileName(output, Persistent, true),
                Is.EqualTo(Path.Combine("review", "frame.png")));
        }

        [Test]
        public void AndroidRejectsOutsidePathsAndLookalikePrefixes()
        {
            Assert.Throws<ArgumentException>(() => SessionRpgCapturePath.ScreenshotFileName(
                Path.Combine(Persistent + "-other", "frame.png"), Persistent, true));
            Assert.Throws<ArgumentException>(() => SessionRpgCapturePath.ScreenshotFileName(
                Path.Combine(Persistent, "..", "frame.png"), Persistent, true));
            Assert.Throws<ArgumentException>(() => SessionRpgCapturePath.ScreenshotFileName(
                Persistent, Persistent, true));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RelativeOutputIsRejected(bool android)
        {
            Assert.Throws<ArgumentException>(() => SessionRpgCapturePath.ScreenshotFileName(
                "frame.png", Persistent, android));
        }

        [Test]
        public void AndroidRejectsRelativePersistentDirectory()
        {
            Assert.Throws<ArgumentException>(() => SessionRpgCapturePath.ScreenshotFileName(
                Path.Combine(Persistent, "frame.png"), "relative-root", true));
        }
    }
}
