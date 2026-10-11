using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class LocalGrindingProgressStoreTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "session-rpg-grinding-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingSave_StartsAtZeroWithoutCreatingACharacterFile()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);

            Assert.That(store.LoadExperience(SpecializationId.Fire), Is.Zero);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }

        [Test]
        public void Save_RoundTripsAcrossStoreRestartAndAtomicReplacement()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            store.SaveExperience(SpecializationId.Fire, 17);
            store.SaveExperience(SpecializationId.Fire, 31);

            LocalGrindingProgressStore restarted = new LocalGrindingProgressStore(directory);
            Assert.That(restarted.LoadExperience(SpecializationId.Fire), Is.EqualTo(31));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
            Assert.That(Path.GetFileName(Directory.GetFiles(directory)[0]), Is.EqualTo("pve-character-v1-2.save"));
        }

        [Test]
        public void EveryPlayableSpecialization_HasAnIndependentSave()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            for (int specialization = 1; specialization <= 7; specialization++)
            {
                store.SaveExperience((SpecializationId)specialization, specialization * 10);
            }

            LocalGrindingProgressStore restarted = new LocalGrindingProgressStore(directory);
            for (int specialization = 1; specialization <= 7; specialization++)
            {
                Assert.That(restarted.LoadExperience((SpecializationId)specialization), Is.EqualTo(specialization * 10));
            }

            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(100)]
        [TestCase(int.MaxValue)]
        public void UnknownAndNonPlayableSpecializations_AreRejected(int specialization)
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);

            Assert.Throws<ArgumentOutOfRangeException>(() => store.LoadExperience((SpecializationId)specialization));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveExperience((SpecializationId)specialization, 0));
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }

        [Test]
        public void NegativeAndBeyondCapExperience_AreRejectedWithoutChangingSavedValue()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            store.SaveExperience(SpecializationId.Fire, 9);

            Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveExperience(SpecializationId.Fire, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveExperience(SpecializationId.Fire, PersistentLevelProgression.MaximumExperience + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveExperience(SpecializationId.Fire, int.MaxValue));
            Assert.That(store.LoadExperience(SpecializationId.Fire), Is.EqualTo(9));
        }

        [Test]
        public void ZeroAndMaximumExperience_RoundTrip()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            store.SaveExperience(SpecializationId.Fire, 0);
            Assert.That(store.LoadExperience(SpecializationId.Fire), Is.Zero);

            store.SaveExperience(SpecializationId.Fire, PersistentLevelProgression.MaximumExperience);
            Assert.That(store.LoadExperience(SpecializationId.Fire), Is.EqualTo(PersistentLevelProgression.MaximumExperience));
        }

        [Test]
        public void EqualExperience_IsIdempotentAndDecreasePreservesExistingFile()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            store.SaveExperience(SpecializationId.Fire, 50);
            byte[] original = File.ReadAllBytes(SavePath);

            store.SaveExperience(SpecializationId.Fire, 50);
            Assert.Throws<InvalidOperationException>(() => store.SaveExperience(SpecializationId.Fire, 49));
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("pve-character-v2\n2\n1\nchecksum")]
        [TestCase("pve-character-v1\n2\n1\nwrong-checksum")]
        [TestCase("pve-character-v1\n2\n1\nchecksum\nextra")]
        public void CorruptAndUnsupportedSave_LoadAndSaveFailClosed(string corrupted)
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            File.WriteAllText(SavePath, corrupted, new UTF8Encoding(false));
            byte[] original = File.ReadAllBytes(SavePath);

            Assert.Throws<InvalidDataException>(() => store.LoadExperience(SpecializationId.Fire));
            Assert.Throws<InvalidDataException>(() => store.SaveExperience(SpecializationId.Fire, 50));
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [TestCase("pve-character-v1\n3\n1")]
        [TestCase("pve-character-v1\n2\n-1")]
        [TestCase("pve-character-v1\n2\n2147483648")]
        [TestCase("pve-character-v1\n2\n01")]
        [TestCase("pve-character-v1\n2\n+1")]
        [TestCase("pve-character-v1\n02\n1")]
        [TestCase("pve-character-v1\r\n2\r\n1")]
        public void ValidChecksum_DoesNotAdmitWrongIdentityOrNonCanonicalValues(string body)
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            WriteChecksummedBody(body);

            Assert.Throws<InvalidDataException>(() => store.LoadExperience(SpecializationId.Fire));
            Assert.Throws<InvalidDataException>(() => store.SaveExperience(SpecializationId.Fire, 50));
        }

        [Test]
        public void BeyondCapOnDisk_IsRejectedEvenWithAValidChecksum()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            WriteChecksummedBody("pve-character-v1\n2\n" + (PersistentLevelProgression.MaximumExperience + 1).ToString(CultureInfo.InvariantCulture));

            Assert.Throws<InvalidDataException>(() => store.LoadExperience(SpecializationId.Fire));
        }

        [Test]
        public void InvalidUtf8AndOversizedSave_AreRejectedWithoutRecovery()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            File.WriteAllBytes(SavePath, new byte[] { 0xff, 0xfe, 0x80 });
            Assert.Throws<InvalidDataException>(() => store.LoadExperience(SpecializationId.Fire));

            File.WriteAllText(SavePath, new string('a', 257));
            Assert.Throws<InvalidDataException>(() => store.LoadExperience(SpecializationId.Fire));
            Assert.That(new FileInfo(SavePath).Length, Is.EqualTo(257));
        }

        [Test]
        public void OrphanTemporaryAndUnrelatedFiles_AreNeverPromotedOrDeleted()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            string orphan = SavePath + ".orphan.tmp";
            string settings = Path.Combine(directory, "settings.json");
            File.WriteAllText(orphan, "uncommitted progress");
            File.WriteAllText(settings, "unrelated data");

            Assert.That(store.LoadExperience(SpecializationId.Fire), Is.Zero);
            store.SaveExperience(SpecializationId.Fire, 20);
            store.SaveExperience(SpecializationId.Fire, 21);
            Assert.That(File.ReadAllText(orphan), Is.EqualTo("uncommitted progress"));
            Assert.That(File.ReadAllText(settings), Is.EqualTo("unrelated data"));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(3));
        }

        [Test]
        public void TwoStores_WritesAreMonotonicAndLeaveNoTemporaryFiles()
        {
            LocalGrindingProgressStore first = new LocalGrindingProgressStore(directory);
            LocalGrindingProgressStore second = new LocalGrindingProgressStore(directory);
            Task firstWrite = Task.Run(() => SaveIncreasing(first, 1, 60));
            Task secondWrite = Task.Run(() => SaveIncreasing(second, 31, 90));
            Task.WaitAll(firstWrite, secondWrite);

            Assert.That(first.LoadExperience(SpecializationId.Fire), Is.EqualTo(90));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public void ChangingCulture_DoesNotChangeFileFormatOrIdentity()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
                store.SaveExperience(SpecializationId.Fire, 123);
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

                Assert.That(store.LoadExperience(SpecializationId.Fire), Is.EqualTo(123));
                Assert.That(File.ReadAllText(SavePath), Does.StartWith("pve-character-v1\n2\n123\n"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void MissingDirectoryArgument_IsRejected(string path)
        {
            Assert.Throws<ArgumentException>(() => new LocalGrindingProgressStore(path));
        }

        [Test]
        public void RemovedSaveDirectory_IsAnIoFailureNotANewCharacter()
        {
            LocalGrindingProgressStore store = new LocalGrindingProgressStore(directory);
            Directory.Delete(directory);

            Assert.Throws<DirectoryNotFoundException>(() => store.LoadExperience(SpecializationId.Fire));
            Assert.Throws<DirectoryNotFoundException>(() => store.SaveExperience(SpecializationId.Fire, 1));
        }

        private string SavePath => Path.Combine(directory, "pve-character-v1-2.save");

        private void WriteChecksummedBody(string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            using (SHA256 algorithm = SHA256.Create())
            {
                string checksum = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                File.WriteAllText(SavePath, body + "\n" + checksum, new UTF8Encoding(false));
            }
        }

        private static void SaveIncreasing(LocalGrindingProgressStore store, int first, int last)
        {
            for (int experience = first; experience <= last; experience++)
            {
                try
                {
                    store.SaveExperience(SpecializationId.Fire, experience);
                }
                catch (InvalidOperationException)
                {
                    // The competing store has already committed a higher value.
                }
            }
        }
    }
}
