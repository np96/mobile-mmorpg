using System.Globalization;
using System.Text.Json;

namespace SessionRpg.Server
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                bool development = false;
                bool provision = false;
                bool rotate = false;
                Guid? playerId = null;
                string database = Path.GetFullPath(Path.Combine(".data", "personal-worlds.db"));
                int port = 5087;
                HashSet<string> seen = new(StringComparer.Ordinal);
                for (int index = 0; index < args.Length; index++)
                {
                    string option = args[index];
                    if (!seen.Add(option)) { throw new ArgumentException("Duplicate option"); }
                    switch (option)
                    {
                        case "--dev-local": development = true; break;
                        case "provision-player": provision = true; break;
                        case "rotate-credential": rotate = true; break;
                        case "--player" when index + 1 < args.Length:
                            playerId = Guid.ParseExact(args[++index], "D"); break;
                        case "--database" when index + 1 < args.Length:
                            database = Path.GetFullPath(args[++index]); break;
                        case "--port" when index + 1 < args.Length:
                            port = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                        default: throw new ArgumentException("Unknown or incomplete option");
                    }
                }
                if (!development)
                {
                    Console.Error.WriteLine("This prototype requires --dev-local and only listens on 127.0.0.1.");
                    return 2;
                }
                if (port < 1 || port > 65535) { throw new ArgumentException("Port must be 1–65535"); }
                if ((provision && rotate) || (rotate != playerId.HasValue))
                {
                    throw new ArgumentException("Use rotate-credential --player GUID to replace a developer credential");
                }
                if (rotate)
                {
                    PlayerCredential credential = new GameStore(database).RotateCredential(playerId!.Value, DateTimeOffset.UtcNow);
                    Console.WriteLine(JsonSerializer.Serialize(credential, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                    return 0;
                }
                if (provision)
                {
                    PlayerCredential credential = new GameStore(database).ProvisionPlayer(DateTimeOffset.UtcNow);
                    Console.WriteLine(JsonSerializer.Serialize(credential, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                    return 0;
                }
                await using WebApplication app = DevelopmentServer.Build(database, port);
                Console.WriteLine($"Local personal-world control plane: http://127.0.0.1:{port} (no networked gameplay).");
                await app.RunAsync();
                return 0;
            }
            catch (Exception exception)
            {
                // Fail closed, preserving saves. Avoid dumping paths or database contents.
                Console.Error.WriteLine($"Server stopped: {exception.GetType().Name}. Check CLI options, SDK and database schema/integrity. Existing saves were not reset.");
                return 1;
            }
        }
    }
}
