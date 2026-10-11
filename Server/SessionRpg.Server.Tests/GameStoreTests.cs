using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace SessionRpg.Server.Tests
{
    public sealed class GameStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "session-rpg-store-tests-" + Guid.NewGuid().ToString("N"));
        private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        private static readonly CreateCharacterRequest ValidRequest = new("  Adventurer  ", 3, 2);
        private string DatabasePath => Path.Combine(_directory, "game.sqlite3");

        [Fact]
        public void ProvisionStoresOnlyCredentialHashAndAuthenticatesUntilExpiry()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            PlayerCredential another = store.ProvisionPlayer(Now);
            Assert.NotEqual(player.PlayerId, another.PlayerId);
            Assert.NotEqual(player.AccessToken, another.AccessToken);
            Assert.Matches("^[0-9a-f]{64}$", player.AccessToken);
            Assert.Equal(Now.AddHours(24), player.ExpiresAt);
            Assert.Equal(player.PlayerId, store.Authenticate(player.AccessToken, Now));
            Assert.Equal(player.PlayerId, store.Authenticate(player.AccessToken, player.ExpiresAt.AddMilliseconds(-1)));
            Assert.Null(store.Authenticate(player.AccessToken, player.ExpiresAt));
            Assert.Null(store.Authenticate(new string('a', 64), Now));
            Assert.Null(store.Authenticate("", Now));
            Assert.Null(store.Authenticate(new string('f', 10000), Now));
            string expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(player.AccessToken))).ToLowerInvariant();
            using SqliteConnection connection = Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT token_hash FROM players WHERE player_id = $player;";
            command.Parameters.AddWithValue("$player", player.PlayerId.ToString("D"));
            Assert.Equal(expectedHash, command.ExecuteScalar());
            ProfileView profile = store.GetProfile(player.PlayerId);
            Assert.Null(profile.Character);
            Assert.Null(profile.World);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 6)]
        [InlineData(2, 4)]
        [InlineData(2, 5)]
        [InlineData(3, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 7)]
        public void EveryPlayableSpecializationCreatesInitialCharacterAndSeparateWorld(int classId, int specializationId)
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            ProfileView profile = store.CreateCharacter(player.PlayerId,
                new CreateCharacterRequest("  Hero  ", classId, specializationId), NewKey());
            CharacterView character = Assert.IsType<CharacterView>(profile.Character);
            WorldView world = Assert.IsType<WorldView>(profile.World);
            Assert.Equal("Hero", character.Name);
            Assert.Equal(classId, character.ClassId);
            Assert.Equal(specializationId, character.SpecializationId);
            Assert.Equal(1, character.Level);
            Assert.Equal(0, character.Experience);
            Assert.Equal(1, character.Revision);
            Assert.Equal(player.PlayerId, character.PlayerId);
            Assert.Equal(player.PlayerId, world.OwnerPlayerId);
            Assert.NotEqual(character.CharacterId, world.PersonalWorldId);
            Assert.NotEqual(Guid.Empty, world.Region.RegionInstanceId);
            Assert.NotEmpty(world.Region.Nodes);
            Assert.NotEmpty(world.Region.Edges);
            Assert.Equal(JsonSerializer.Serialize(world), JsonSerializer.Serialize(store.GetOwnedWorld(player.PlayerId, world.PersonalWorldId)));
        }

        [Theory]
        [InlineData(100, 100)]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(3, 100)]
        [InlineData(-1, 1)]
        [InlineData(500, 500)]
        public void InvalidOrNpcClassCannotCreateCharacter(int classId, int specializationId)
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            ApiFault fault = Assert.Throws<ApiFault>(() => store.CreateCharacter(player.PlayerId,
                new CreateCharacterRequest("Hero", classId, specializationId), NewKey()));
            Assert.Equal(400, fault.StatusCode);
            Assert.Null(store.GetProfile(player.PlayerId).Character);
            Assert.Equal(0L, Count("personal_worlds"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        [InlineData("Name\nControl")]
        [InlineData("Name\0Control")]
        [InlineData("123456789012345678901234567890123")]
        public void InvalidNameDoesNotMutateState(string name)
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            Assert.Equal(400, Assert.Throws<ApiFault>(() => store.CreateCharacter(player.PlayerId,
                ValidRequest with { Name = name }, NewKey())).StatusCode);
            Assert.Equal(0L, Count("characters"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-id")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
        [InlineData("aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa")]
        public void IdempotencyKeyMustBeCanonicalNonemptyGuid(string key)
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            ApiFault fault = Assert.Throws<ApiFault>(() => store.CreateCharacter(player.PlayerId, ValidRequest, key));
            Assert.Equal(400, fault.StatusCode);
            Assert.Equal("invalid_idempotency_key", fault.Code);
            Assert.Equal(0L, Count("characters"));
        }

        [Fact]
        public void OperatorCredentialRotationRevokesOldTokenAndRetainsProfileAfterRestart()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            PlayerCredential another = store.ProvisionPlayer(Now);
            string profile = JsonSerializer.Serialize(store.CreateCharacter(player.PlayerId, ValidRequest, NewKey()));
            PlayerCredential replacement = store.RotateCredential(player.PlayerId, Now.AddHours(1));
            Assert.Equal(player.PlayerId, replacement.PlayerId);
            Assert.Equal(Now.AddHours(25), replacement.ExpiresAt);
            Assert.NotEqual(player.AccessToken, replacement.AccessToken);
            Assert.Null(store.Authenticate(player.AccessToken, Now.AddHours(1)));
            Assert.Equal(player.PlayerId, store.Authenticate(replacement.AccessToken, Now.AddHours(1)));
            Assert.Equal(another.PlayerId, store.Authenticate(another.AccessToken, Now.AddHours(1)));
            var restarted = new GameStore(DatabasePath);
            Assert.Null(restarted.Authenticate(player.AccessToken, Now.AddHours(1)));
            Assert.Equal(player.PlayerId, restarted.Authenticate(replacement.AccessToken, Now.AddHours(1)));
            Assert.Equal(profile, JsonSerializer.Serialize(restarted.GetProfile(player.PlayerId)));
        }

        [Fact]
        public void OperatorCanRecoverExpiredCredentialButCannotRotateMissingPlayer()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            Assert.Equal(404, Assert.Throws<ApiFault>(() => store.RotateCredential(Guid.NewGuid(), Now)).StatusCode);
            Assert.Equal(player.PlayerId, store.Authenticate(player.AccessToken, Now));
            Assert.Equal(1L, Count("players"));
            PlayerCredential replacement = store.RotateCredential(player.PlayerId, Now.AddDays(2));
            Assert.Equal(player.PlayerId, store.Authenticate(replacement.AccessToken, Now.AddDays(2)));
            Assert.Null(store.Authenticate(player.AccessToken, Now.AddDays(2)));
        }

        [Fact]
        public void RestartAndNormalizedRetryReturnTheExactSavedCreation()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            string key = NewKey();
            ProfileView created = store.CreateCharacter(player.PlayerId, ValidRequest, key);
            string original = JsonSerializer.Serialize(created);
            var restarted = new GameStore(DatabasePath);
            Assert.Equal(player.PlayerId, restarted.Authenticate(player.AccessToken, Now));
            Assert.Equal(original, JsonSerializer.Serialize(restarted.GetProfile(player.PlayerId)));
            Assert.Equal(original, JsonSerializer.Serialize(restarted.CreateCharacter(player.PlayerId,
                ValidRequest with { Name = "Adventurer" }, key)));
            Assert.Equal(1L, Count("characters"));
            Assert.Equal(1L, Count("personal_worlds"));
            Assert.Equal(1L, Count("creation_receipts"));
        }

        [Fact]
        public void ReusedKeyWithDifferentPayloadAndNewKeyAfterCreationBothConflict()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            string key = NewKey();
            ProfileView created = store.CreateCharacter(player.PlayerId, ValidRequest, key);
            ApiFault reuse = Assert.Throws<ApiFault>(() => store.CreateCharacter(player.PlayerId,
                ValidRequest with { Name = "Different" }, key));
            Assert.Equal(409, reuse.StatusCode);
            Assert.Equal("idempotency_conflict", reuse.Code);
            ApiFault second = Assert.Throws<ApiFault>(() => store.CreateCharacter(player.PlayerId, ValidRequest, NewKey()));
            Assert.Equal(409, second.StatusCode);
            Assert.Equal("character_already_exists", second.Code);
            Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(store.GetProfile(player.PlayerId)));
        }

        [Fact]
        public void WorldLookupNeverDisclosesAnotherOwnersWorld()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential owner = store.ProvisionPlayer(Now);
            PlayerCredential visitor = store.ProvisionPlayer(Now);
            Guid world = store.CreateCharacter(owner.PlayerId, ValidRequest, NewKey()).World!.PersonalWorldId;
            ApiFault denied = Assert.Throws<ApiFault>(() => store.GetOwnedWorld(visitor.PlayerId, world));
            ApiFault missing = Assert.Throws<ApiFault>(() => store.GetOwnedWorld(visitor.PlayerId, Guid.NewGuid()));
            Assert.Equal(404, denied.StatusCode);
            Assert.Equal(missing.Code, denied.Code);
            Assert.Equal(404, Assert.Throws<ApiFault>(() => store.GetProfile(Guid.NewGuid())).StatusCode);
            Assert.Equal(404, Assert.Throws<ApiFault>(() => store.CreateCharacter(Guid.NewGuid(), ValidRequest, NewKey())).StatusCode);
        }

        [Fact]
        public async Task ConcurrentSameKeyCreationProducesExactlyOneSavedWorld()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            string key = NewKey();
            ProfileView[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                Task.Run(() => store.CreateCharacter(player.PlayerId, ValidRequest, key))));
            Assert.Single(results.Select(result => JsonSerializer.Serialize(result)).Distinct());
            Assert.Equal(1L, Count("characters"));
            Assert.Equal(1L, Count("personal_worlds"));
            Assert.Equal(1L, Count("creation_receipts"));
        }

        [Fact]
        public async Task ConcurrentDifferentKeysProduceOneCharacterAndConflicts()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            int[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                try
                {
                    store.CreateCharacter(player.PlayerId, ValidRequest, NewKey());
                    return 201;
                }
                catch (ApiFault fault)
                {
                    return fault.StatusCode;
                }
            })));
            Assert.Equal(1, results.Count(status => status == 201));
            Assert.Equal(7, results.Count(status => status == 409));
            Assert.Equal(1L, Count("characters"));
            Assert.Equal(1L, Count("personal_worlds"));
        }

        [Fact]
        public void WorldInsertFailureRollsBackCharacterAndRetryReceipt()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            Execute("CREATE TRIGGER reject_world BEFORE INSERT ON personal_worlds BEGIN SELECT RAISE(ABORT, 'injected persistence failure'); END;");
            string key = NewKey();
            Assert.Throws<SqliteException>(() => store.CreateCharacter(player.PlayerId, ValidRequest, key));
            Assert.Equal(0L, Count("characters"));
            Assert.Equal(0L, Count("personal_worlds"));
            Assert.Equal(0L, Count("creation_receipts"));
            Execute("DROP TRIGGER reject_world;");
            Assert.NotNull(store.CreateCharacter(player.PlayerId, ValidRequest, key).Character);
        }

        [Fact]
        public void SavedCompleteRegionPlanIsReadWithoutRegeneratingFromSeed()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            WorldView world = store.CreateCharacter(player.PlayerId, ValidRequest, NewKey()).World!;
            RegionPlan saved = world.Region with { Seed = 987654321, GenerationVersion = 77, ContentVersion = 88 };
            using (SqliteConnection connection = Connect())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE personal_worlds SET region_json = $json WHERE world_id = $world;";
                command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(saved));
                command.Parameters.AddWithValue("$world", world.PersonalWorldId.ToString("D"));
                command.ExecuteNonQuery();
            }

            var restarted = new GameStore(DatabasePath);
            RegionPlan loaded = restarted.GetOwnedWorld(player.PlayerId, world.PersonalWorldId).Region;
            Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(loaded));
        }

        [Fact]
        public void CorruptDatabaseIsNotOverwritten()
        {
            Directory.CreateDirectory(_directory);
            byte[] original = Encoding.UTF8.GetBytes("This is a corrupt save, not a SQLite database.");
            File.WriteAllBytes(DatabasePath, original);
            Assert.Throws<InvalidOperationException>(() => new GameStore(DatabasePath));
            Assert.Equal(original, File.ReadAllBytes(DatabasePath));
        }

        [Fact]
        public void UnknownVersionFailsClosedAndPreservesData()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            store.CreateCharacter(player.PlayerId, ValidRequest, NewKey());
            Execute("PRAGMA user_version = 99;");
            Assert.Throws<InvalidOperationException>(() => new GameStore(DatabasePath));
            Assert.Equal(1L, Count("characters"));
            Assert.Equal(99L, Query("PRAGMA user_version;"));
        }

        [Fact]
        public void NonemptyUnversionedDatabaseIsNeverInitialized()
        {
            Directory.CreateDirectory(_directory);
            Execute("CREATE TABLE existing_save(value INTEGER); INSERT INTO existing_save VALUES(42);");
            Assert.Throws<InvalidOperationException>(() => new GameStore(DatabasePath));
            Assert.Equal(42L, Query("SELECT value FROM existing_save;"));
            Assert.Equal(0L, Query("PRAGMA user_version;"));
            Assert.Equal(0L, Query("SELECT COUNT(*) FROM sqlite_master WHERE name = 'players';"));
        }

        [Fact]
        public void MissingVersionOneSchemaFailsClosed()
        {
            Directory.CreateDirectory(_directory);
            Execute("PRAGMA user_version = 1;");
            Assert.Throws<InvalidOperationException>(() => new GameStore(DatabasePath));
            Assert.Equal(0L, Query("SELECT COUNT(*) FROM sqlite_master WHERE name = 'players';"));
        }

        [Fact]
        public void InvalidSavedRegionJsonFailsWithoutInventingNewGeography()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            Guid world = store.CreateCharacter(player.PlayerId, ValidRequest, NewKey()).World!.PersonalWorldId;
            Execute("UPDATE personal_worlds SET region_json = 'corrupt-json';");
            Assert.Throws<InvalidOperationException>(() => store.GetOwnedWorld(player.PlayerId, world));
            Assert.Equal("corrupt-json", Query("SELECT region_json FROM personal_worlds;"));
        }

        [Fact]
        public void IncompleteSavedRegionCannotBecomeAnEmptyGeneratedWorld()
        {
            var store = new GameStore(DatabasePath);
            PlayerCredential player = store.ProvisionPlayer(Now);
            Guid world = store.CreateCharacter(player.PlayerId, ValidRequest, NewKey()).World!.PersonalWorldId;
            Execute("UPDATE personal_worlds SET region_json = '{}';");
            Assert.Throws<InvalidOperationException>(() => store.GetOwnedWorld(player.PlayerId, world));
            Assert.Equal("{}", Query("SELECT region_json FROM personal_worlds;"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static string NewKey() => Guid.NewGuid().ToString("D");

        private SqliteConnection Connect()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Pooling = false,
            }.ToString());
            connection.Open();
            return connection;
        }

        private void Execute(string sql)
        {
            using SqliteConnection connection = Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private object? Query(string sql)
        {
            using SqliteConnection connection = Connect();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }

        private long Count(string table) => Convert.ToInt64(Query("SELECT COUNT(*) FROM " + table + ";"));
    }
}
