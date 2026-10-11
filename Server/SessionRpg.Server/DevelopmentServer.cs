using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SessionRpg.Server
{
    // Explicitly development-only HTTP control plane. Never a public identity provider.
    public static class DevelopmentServer
    {
        public static WebApplication Build(string databasePath, int port = 5087,
            TimeProvider? clock = null, IConfiguration? configuration = null)
        {
            if (port < 0 || port > 65535) { throw new ArgumentOutOfRangeException(nameof(port)); }
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                EnvironmentName = Environments.Production
            });
            if (configuration != null)
            {
                // Allows isolated configuration tests without mutating process
                // environment. Endpoint policy below applies to every source.
                builder.Configuration.AddConfiguration(configuration);
            }
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}").PreferHostingUrls(false);
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Listen() adds a code endpoint; it does NOT remove endpoints
                // inherited from appsettings or Kestrel__Endpoints__* variables.
                // Replace that loader, including its reload source, so config
                // cannot silently turn this development server into a LAN host.
                options.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
                options.Listen(IPAddress.Loopback, port);
                options.Limits.MaxRequestBodySize = 16 * 1024;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            });
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.SerializerOptions.MaxDepth = 16;
            });
            builder.Services.AddSingleton(new GameStore(databasePath));
            builder.Services.AddSingleton(clock ?? TimeProvider.System);
            builder.Services.AddSingleton<SessionService>();
            builder.Services.AddAuthentication("LocalDeveloperToken")
                .AddScheme<AuthenticationSchemeOptions, DeveloperTokenHandler>("LocalDeveloperToken", null);
            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetFixedWindowLimiter("local-development", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            });
            WebApplication app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                try
                {
                    await next(context);
                }
                catch (ApiFault fault)
                {
                    context.Response.StatusCode = fault.StatusCode;
                    await context.Response.WriteAsJsonAsync(new ErrorView(fault.Code));
                }
                catch (BadHttpRequestException fault)
                {
                    context.Response.StatusCode = fault.StatusCode;
                    await context.Response.WriteAsJsonAsync(new ErrorView("invalid_request"));
                }
                catch (SqliteException fault) when (fault.SqliteErrorCode is 5 or 6)
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsJsonAsync(new ErrorView("storage_busy"));
                }
                catch (Exception fault)
                {
                    // Never include request bodies, credentials, or persistence contents.
                    app.Logger.LogError("Request failed with {ExceptionType}", fault.GetType().Name);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new ErrorView("internal_error"));
                }
            });
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "local-development", gameplay = false }));
            RouteGroupBuilder api = app.MapGroup("/v1").RequireAuthorization();
            api.MapGet("/me", (HttpContext context, GameStore store) => store.GetProfile(Player(context)));
            api.MapPost("/characters", (HttpContext context, GameStore store, CreateCharacterRequest request) =>
            {
                string key = context.Request.Headers["Idempotency-Key"].ToString();
                return store.CreateCharacter(Player(context), request, key);
            });
            api.MapGet("/worlds/{worldId:guid}", (HttpContext context, GameStore store, Guid worldId) =>
                store.GetOwnedWorld(Player(context), worldId));
            api.MapPost("/sessions", (HttpContext context, SessionService service, StartSessionRequest request) =>
                service.Start(Player(context), request));
            api.MapGet("/sessions/{sessionId:guid}", (HttpContext context, SessionService service, Guid sessionId) =>
                service.Get(Player(context), sessionId));
            api.MapPost("/sessions/{sessionId:guid}/invites", (HttpContext context, SessionService service,
                Guid sessionId, ExpectedRevisionRequest request) =>
                service.CreateInvite(Player(context), sessionId, request.ExpectedRevision));
            api.MapPost("/sessions/join", (HttpContext context, SessionService service, JoinSessionRequest request) =>
                service.Join(Player(context), request));
            api.MapPost("/sessions/{sessionId:guid}/leave", (HttpContext context, SessionService service,
                Guid sessionId, ExpectedRevisionRequest request) =>
                service.Leave(Player(context), sessionId, request.ExpectedRevision));
            return app;
        }

        private static Guid Player(HttpContext context) =>
            Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    internal sealed class DeveloperTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly GameStore store;
        private readonly TimeProvider clock;

        public DeveloperTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, GameStore store, TimeProvider clock)
            : base(options, logger, encoder)
        {
            this.store = store;
            this.clock = clock;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            string token = authorization[7..];
            Guid? player = store.Authenticate(token, clock.GetUtcNow());
            if (player == null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid developer credential"));
            }
            ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, player.Value.ToString("D"))], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
