using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SessionRpg.Core;

namespace SessionRpg.Server
{
    /// <summary>
    /// Single-machine prototype persistence, not a distributed account service.
    /// Character creation, its independent world, and the retry receipt commit together.
    /// No API accepts client-authored experience, loot, or world-completion claims.
    /// </summary>
    public sealed class GameStore
    {
        private const int SchemaVersion = 1;
        private static readonly JsonSerializerOptions SavedJsonOptions = new()
        {
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
        };
        private readonly string _connectionString;

        public GameStore(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            string fullPath = Path.GetFullPath(databasePath);
            bool existingNonemptyFile = File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = false,
                DefaultTimeout = 5,
            }.ToString();

            try
            {
                using SqliteConnection connection = Open();
                using (SqliteTransaction transaction = connection.BeginTransaction(deferred: false))
                {
                    long version = Convert.ToInt64(Scalar(connection, transaction, "PRAGMA user_version;"));
                    if (version == 0)
                    {
                        long tableCount = Convert.ToInt64(Scalar(connection, transaction,
                            "SELECT COUNT(*) FROM sqlite_master;"));
                        if (existingNonemptyFile || tableCount != 0)
                        {
                            throw new InvalidOperationException("Refusing to initialize a nonempty unversioned database.");
                        }

                        Execute(connection, transaction, """
                            CREATE TABLE players (
                                player_id TEXT NOT NULL PRIMARY KEY,
                                token_hash TEXT NOT NULL UNIQUE CHECK(length(token_hash) = 64),
                                token_expires_at INTEGER NOT NULL
                            );
                            CREATE TABLE characters (
                                character_id TEXT NOT NULL PRIMARY KEY,
                                player_id TEXT NOT NULL UNIQUE REFERENCES players(player_id),
                                name TEXT NOT NULL,
                                class_id INTEGER NOT NULL,
                                specialization_id INTEGER NOT NULL,
                                level INTEGER NOT NULL CHECK(level >= 1),
                                experience INTEGER NOT NULL CHECK(experience >= 0),
                                revision INTEGER NOT NULL CHECK(revision > 0)
                            );
                            CREATE TABLE personal_worlds (
                                world_id TEXT NOT NULL PRIMARY KEY,
                                owner_player_id TEXT NOT NULL UNIQUE REFERENCES players(player_id),
                                region_json TEXT NOT NULL,
                                revision INTEGER NOT NULL CHECK(revision > 0)
                            );
                            CREATE TABLE creation_receipts (
                                player_id TEXT NOT NULL REFERENCES players(player_id),
                                idempotency_key TEXT NOT NULL,
                                request_json TEXT NOT NULL,
                                response_json TEXT NOT NULL,
                                PRIMARY KEY(player_id, idempotency_key)
                            );
                            PRAGMA user_version = 1;
                            """);
                    }
                    else if (version != SchemaVersion)
                    {
                        throw new InvalidOperationException($"Unsupported database schema version {version}; expected {SchemaVersion}.");
                    }

                    if (!string.Equals(Convert.ToString(Scalar(connection, transaction,
                            "PRAGMA integrity_check;")), "ok", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Database integrity check failed; the save was not reset.");
                    }

                    if (Scalar(connection, transaction, "PRAGMA foreign_key_check;") != null)
                    {
                        throw new InvalidOperationException("Database foreign-key check failed; the save was not reset.");
                    }

                    // Fail closed on a version-one file missing any expected schema.
                    Execute(connection, transaction, """
                        SELECT player_id, token_hash, token_expires_at FROM players LIMIT 0;
                        SELECT character_id, player_id, name, class_id, specialization_id,
                            level, experience, revision FROM characters LIMIT 0;
                        SELECT world_id, owner_player_id, region_json, revision FROM personal_worlds LIMIT 0;
                        SELECT player_id, idempotency_key, request_json, response_json FROM creation_receipts LIMIT 0;
                        """);
                    transaction.Commit();
                }

                Execute(connection, null, "PRAGMA journal_mode = WAL;");
            }
            catch (SqliteException exception)
            {
                throw new InvalidOperationException("Could not open the game database; no save reset was attempted.", exception);
            }
        }

        public PlayerCredential ProvisionPlayer(DateTimeOffset now)
        {
            Guid playerId = Guid.NewGuid();
            PlayerCredential credential = NewCredential(playerId, now);
            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            Execute(connection, transaction,
                "INSERT INTO players(player_id, token_hash, token_expires_at) VALUES($player, $hash, $expires);",
                ("$player", Key(playerId)), ("$hash", Hash(credential.AccessToken)),
                ("$expires", credential.ExpiresAt.ToUnixTimeMilliseconds()));
            transaction.Commit();
            return credential;
        }

        /// <summary>
        /// Local operator-only recovery for expiring development credentials. This is
        /// not exposed by the HTTP API and does not alter character or world progress.
        /// </summary>
        public PlayerCredential RotateCredential(Guid playerId, DateTimeOffset now)
        {
            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            RequirePlayer(connection, transaction, playerId);
            PlayerCredential credential = NewCredential(playerId, now);
            Execute(connection, transaction,
                "UPDATE players SET token_hash = $hash, token_expires_at = $expires WHERE player_id = $player;",
                ("$player", Key(playerId)), ("$hash", Hash(credential.AccessToken)),
                ("$expires", credential.ExpiresAt.ToUnixTimeMilliseconds()));
            transaction.Commit();
            return credential;
        }

        public Guid? Authenticate(string token, DateTimeOffset now)
        {
            if (token == null || token.Length != 64 || token.Any(character =>
                    !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
            {
                return null;
            }

            using SqliteConnection connection = Open();
            object? value = Scalar(connection, null,
                "SELECT player_id FROM players WHERE token_hash = $hash AND token_expires_at > $now;",
                ("$hash", Hash(token)), ("$now", now.ToUnixTimeMilliseconds()));
            return value is string id ? Guid.ParseExact(id, "D") : null;
        }

        public ProfileView GetProfile(Guid playerId)
        {
            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
            RequirePlayer(connection, transaction, playerId);
            CharacterView? character = ReadCharacter(connection, transaction, playerId);
            WorldView? world = ReadWorld(connection, transaction, playerId, null);
            transaction.Commit();
            return new ProfileView(playerId, character, world);
        }

        public ProfileView CreateCharacter(Guid playerId, CreateCharacterRequest request, string idempotencyKey)
        {
            if (!Guid.TryParseExact(idempotencyKey, "D", out Guid parsedKey) || parsedKey == Guid.Empty ||
                !string.Equals(idempotencyKey, parsedKey.ToString("D"), StringComparison.Ordinal))
            {
                throw new ApiFault(400, "invalid_idempotency_key");
            }

            CreateCharacterRequest normalized = Normalize(request);
            string requestJson = JsonSerializer.Serialize(normalized);
            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            RequirePlayer(connection, transaction, playerId);
            using (SqliteCommand receiptCommand = Command(connection, transaction,
                       "SELECT request_json, response_json FROM creation_receipts WHERE player_id = $player AND idempotency_key = $key;",
                       ("$player", Key(playerId)), ("$key", idempotencyKey)))
            using (SqliteDataReader receipt = receiptCommand.ExecuteReader())
            {
                if (receipt.Read())
                {
                    if (!string.Equals(requestJson, receipt.GetString(0), StringComparison.Ordinal))
                    {
                        throw new ApiFault(409, "idempotency_conflict");
                    }

                    return Deserialize<ProfileView>(receipt.GetString(1));
                }
            }

            if (ReadCharacter(connection, transaction, playerId) != null ||
                ReadWorld(connection, transaction, playerId, null) != null)
            {
                throw new ApiFault(409, "character_already_exists");
            }

            var character = new CharacterView(Guid.NewGuid(), playerId, normalized.Name,
                normalized.ClassId, normalized.SpecializationId, 1, 0, 1);
            var world = new WorldView(Guid.NewGuid(), playerId,
                RegionPlanGenerator.Create(Guid.NewGuid(), RandomNumberGenerator.GetInt32(int.MaxValue)), 1);
            var profile = new ProfileView(playerId, character, world);

            Execute(connection, transaction, """
                INSERT INTO characters(character_id, player_id, name, class_id, specialization_id, level, experience, revision)
                VALUES($character, $player, $name, $class, $spec, 1, 0, 1);
                """, ("$character", Key(character.CharacterId)), ("$player", Key(playerId)),
                ("$name", character.Name), ("$class", character.ClassId), ("$spec", character.SpecializationId));
            Execute(connection, transaction,
                "INSERT INTO personal_worlds(world_id, owner_player_id, region_json, revision) VALUES($world, $player, $region, 1);",
                ("$world", Key(world.PersonalWorldId)), ("$player", Key(playerId)),
                ("$region", JsonSerializer.Serialize(world.Region)));
            Execute(connection, transaction, """
                INSERT INTO creation_receipts(player_id, idempotency_key, request_json, response_json)
                VALUES($player, $key, $request, $response);
                """, ("$player", Key(playerId)), ("$key", idempotencyKey),
                ("$request", requestJson), ("$response", JsonSerializer.Serialize(profile)));
            transaction.Commit();
            return profile;
        }

        public WorldView GetOwnedWorld(Guid playerId, Guid worldId)
        {
            using SqliteConnection connection = Open();
            return ReadWorld(connection, null, playerId, worldId) ?? throw new ApiFault(404, "world_not_found");
        }

        private static CreateCharacterRequest Normalize(CreateCharacterRequest request)
        {
            string name = request?.Name?.Trim() ?? string.Empty;
            if (request == null || name.Length is < 1 or > 32 || name.Any(char.IsControl) ||
                request.ClassId is not ((int)ClassId.Priest) and not ((int)ClassId.Rogue) and
                    not ((int)ClassId.Mage) and not ((int)ClassId.Hunter) ||
                !SpecializationCatalog.IsCompatible((ClassId)request.ClassId, (SpecializationId)request.SpecializationId))
            {
                throw new ApiFault(400, "invalid_character");
            }

            return request with { Name = name };
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                connection.Open();
                Execute(connection, null, "PRAGMA busy_timeout = 5000; PRAGMA synchronous = FULL;");
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static void RequirePlayer(SqliteConnection connection, SqliteTransaction? transaction, Guid playerId)
        {
            if (Scalar(connection, transaction, "SELECT 1 FROM players WHERE player_id = $player;",
                    ("$player", Key(playerId))) == null)
            {
                throw new ApiFault(404, "player_not_found");
            }
        }

        private static CharacterView? ReadCharacter(SqliteConnection connection, SqliteTransaction? transaction, Guid playerId)
        {
            using SqliteCommand command = Command(connection, transaction, """
                SELECT character_id, name, class_id, specialization_id, level, experience, revision
                FROM characters WHERE player_id = $player;
                """, ("$player", Key(playerId)));
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? new CharacterView(Guid.ParseExact(reader.GetString(0), "D"), playerId,
                reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
                reader.GetInt64(5), reader.GetInt64(6)) : null;
        }

        private static WorldView? ReadWorld(SqliteConnection connection, SqliteTransaction? transaction, Guid playerId, Guid? worldId)
        {
            using SqliteCommand command = Command(connection, transaction, """
                SELECT world_id, region_json, revision FROM personal_worlds
                WHERE owner_player_id = $player AND ($world IS NULL OR world_id = $world);
                """, ("$player", Key(playerId)), ("$world", worldId.HasValue ? Key(worldId.Value) : DBNull.Value));
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? new WorldView(Guid.ParseExact(reader.GetString(0), "D"), playerId,
                Deserialize<RegionPlan>(reader.GetString(1)), reader.GetInt64(2)) : null;
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, SavedJsonOptions) ??
                    throw new InvalidOperationException("Saved record is null; no save reset was attempted.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Saved record is corrupt; no save reset was attempted.", exception);
            }
        }

        private static string Key(Guid value) => value.ToString("D");

        private static PlayerCredential NewCredential(Guid playerId, DateTimeOffset now) => new(
            playerId, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            DateTimeOffset.FromUnixTimeMilliseconds(now.AddHours(24).ToUnixTimeMilliseconds()));

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction,
            string sql, params (string Name, object Value)[] parameters)
        {
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach ((string name, object value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            return command;
        }

        private static object? Scalar(SqliteConnection connection, SqliteTransaction? transaction,
            string sql, params (string Name, object Value)[] parameters)
        {
            using SqliteCommand command = Command(connection, transaction, sql, parameters);
            return command.ExecuteScalar();
        }

        private static void Execute(SqliteConnection connection, SqliteTransaction? transaction,
            string sql, params (string Name, object Value)[] parameters)
        {
            using SqliteCommand command = Command(connection, transaction, sql, parameters);
            command.ExecuteNonQuery();
        }
    }
}
