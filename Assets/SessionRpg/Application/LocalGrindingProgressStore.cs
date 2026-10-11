using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>
    /// Device-local prototype XP, deliberately separate from settings and Training.
    /// The checksum detects accidental damage; it is not anti-cheat or server trust.
    /// Calls perform synchronous disk work and must remain outside simulation ticks.
    /// </summary>
    public sealed class LocalGrindingProgressStore
    {
        private const string FormatVersion = "pve-character-v1";
        private const int MaximumFileBytes = 256;
        private static readonly object SaveGate = new object();
        private static readonly Encoding FileEncoding = new UTF8Encoding(false, true);
        private readonly string directory;

        public LocalGrindingProgressStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A private save directory is required.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(this.directory);
        }

        public int LoadExperience(SpecializationId specialization)
        {
            string path = GetSavePath(specialization);
            lock (SaveGate)
            {
                TryReadExperience(path, specialization, out int experience);
                return experience;
            }
        }

        public void SaveExperience(SpecializationId specialization, int experience)
        {
            string path = GetSavePath(specialization);
            if (experience < 0 || experience > PersistentLevelProgression.MaximumExperience)
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }

            lock (SaveGate)
            {
                // Existing data is validated even for an equal-value write. Never
                // normalize corruption into a new character or overwrite a bad save.
                bool exists = TryReadExperience(path, specialization, out int previous);
                if (experience < previous)
                {
                    throw new InvalidOperationException("Persistent PvE experience cannot decrease.");
                }

                if (exists && experience == previous)
                {
                    return;
                }

                string body = CreateBody(specialization, experience);
                byte[] bytes = FileEncoding.GetBytes(body + "\n" + ComputeChecksum(body));
                string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (FileStream output = new FileStream(
                        temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        output.Write(bytes, 0, bytes.Length);
                        output.Flush(true);
                    }

                    if (exists)
                    {
                        // No delete-and-move fallback: unsupported atomic replacement
                        // must surface a save failure while preserving the old file.
                        File.Replace(temporaryPath, path, null);
                    }
                    else
                    {
                        File.Move(temporaryPath, path);
                    }
                }
                finally
                {
                    // Only this invocation's temporary file belongs to this store.
                    // A crash orphan is never promoted or globally deleted on load.
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }

        private string GetSavePath(SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                case SpecializationId.Fire:
                case SpecializationId.Frost:
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                case SpecializationId.Shadow:
                case SpecializationId.Marksmanship:
                    return Path.Combine(directory, FormatVersion + "-" +
                        ((int)specialization).ToString(CultureInfo.InvariantCulture) + ".save");
                default:
                    throw new ArgumentOutOfRangeException(nameof(specialization));
            }
        }

        private static bool TryReadExperience(
            string path, SpecializationId specialization, out int experience)
        {
            experience = 0;
            byte[] bytes;
            try
            {
                using (FileStream input = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (input.Length == 0 || input.Length > MaximumFileBytes)
                    {
                        throw new InvalidDataException("Invalid PvE progress file length.");
                    }

                    bytes = new byte[(int)input.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int count = input.Read(bytes, offset, bytes.Length - offset);
                        if (count == 0)
                        {
                            throw new InvalidDataException("Incomplete PvE progress file.");
                        }

                        offset += count;
                    }

                    if (input.ReadByte() != -1)
                    {
                        throw new InvalidDataException("PvE progress file changed during reading.");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            string contents;
            try
            {
                contents = FileEncoding.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Invalid PvE progress encoding.", exception);
            }

            string[] lines = contents.Split('\n');
            if (lines.Length != 4 || lines[0] != FormatVersion ||
                lines[1] != ((int)specialization).ToString(CultureInfo.InvariantCulture) ||
                !int.TryParse(lines[2], NumberStyles.None, CultureInfo.InvariantCulture, out experience) ||
                experience < 0 || experience > PersistentLevelProgression.MaximumExperience)
            {
                throw new InvalidDataException("Invalid or unsupported PvE progress data.");
            }

            string body = CreateBody(specialization, experience);
            if (!string.Equals(contents, body + "\n" + ComputeChecksum(body), StringComparison.Ordinal))
            {
                throw new InvalidDataException("PvE progress checksum or canonical format mismatch.");
            }

            return true;
        }

        private static string CreateBody(SpecializationId specialization, int experience)
        {
            return FormatVersion + "\n" +
                ((int)specialization).ToString(CultureInfo.InvariantCulture) + "\n" +
                experience.ToString(CultureInfo.InvariantCulture);
        }

        private static string ComputeChecksum(string body)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(FileEncoding.GetBytes(body));
                StringBuilder result = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    result.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }
    }
}
