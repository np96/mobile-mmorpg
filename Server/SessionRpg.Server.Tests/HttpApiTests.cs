using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SessionRpg.Server.Tests
{
    public sealed class HttpApiTests
    {
        [Fact]
        public async Task HealthIsPublicButPlayerDataRequiresAnActualBearerSecret()
        {
            await using Fixture fixture = await Fixture.Start();
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/health")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/v1/me")).StatusCode);
            PlayerCredential player = fixture.Provision();
            fixture.Authenticate(player.PlayerId.ToString());
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/v1/me")).StatusCode);
            fixture.Authenticate(player.AccessToken);
            ProfileView? profile = await fixture.Client.GetFromJsonAsync<ProfileView>("/v1/me");
            Assert.Equal(player.PlayerId, profile!.PlayerId);
            Assert.Null(profile.Character);
        }

        [Fact]
        public async Task CharacterCreationRequiresIdempotencyAndRejectsClientOwnedProgression()
        {
            await using Fixture fixture = await Fixture.Start();
            fixture.Authenticate(fixture.Provision().AccessToken);
            const string valid = "{\"name\":\"Ember\",\"classId\":3,\"specializationId\":2}";
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Post("/v1/characters", valid)).StatusCode);
            string key = Guid.NewGuid().ToString();
            foreach (string extra in new[] { "\"experience\":9999", "\"level\":60", "\"ownerPlayerId\":\"fake\"", "\"items\":[]" })
            {
                string malicious = valid[..^1] + "," + extra + "}";
                Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Post("/v1/characters", malicious, key)).StatusCode);
            }
            HttpResponseMessage first = await fixture.Post("/v1/characters", valid, key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            string saved = await first.Content.ReadAsStringAsync();
            HttpResponseMessage repeated = await fixture.Post("/v1/characters", valid, key);
            Assert.Equal(saved, await repeated.Content.ReadAsStringAsync());
            ProfileView profile = (await first.Content.ReadFromJsonAsync<ProfileView>())!;
            Assert.Equal(1, profile.Character!.Level);
            Assert.Equal(0, profile.Character.Experience);
            Assert.Equal(HttpStatusCode.Conflict, (await fixture.Post("/v1/characters", valid, Guid.NewGuid().ToString())).StatusCode);
        }

        [Fact]
        public async Task OwnerAndGuestFlowNeverExposesOrModifiesGuestsHomeWorld()
        {
            await using Fixture fixture = await Fixture.Start();
            PlayerCredential owner = fixture.Provision();
            PlayerCredential guest = fixture.Provision();
            ProfileView ownerProfile = await fixture.Create(owner, "Owner");
            ProfileView guestProfile = await fixture.Create(guest, "Guest");
            fixture.Authenticate(guest.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/v1/worlds/{ownerProfile.World!.PersonalWorldId}")).StatusCode);
            StartSessionRequest start = new(ownerProfile.Character!.CharacterId,
                ownerProfile.World.PersonalWorldId, ownerProfile.World.Region.RegionInstanceId);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/v1/sessions", start)).StatusCode);
            fixture.Authenticate(owner.AccessToken);
            SessionView session = await Read<SessionView>(await fixture.Client.PostAsJsonAsync("/v1/sessions", start));
            InviteView invite = await Read<InviteView>(await fixture.Client.PostAsJsonAsync(
                $"/v1/sessions/{session.SessionId}/invites", new ExpectedRevisionRequest(session.Revision)));
            fixture.Authenticate(guest.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/v1/sessions/{session.SessionId}")).StatusCode);
            SessionView joined = await Read<SessionView>(await fixture.Client.PostAsJsonAsync("/v1/sessions/join",
                new JoinSessionRequest(guestProfile.Character!.CharacterId, invite.InviteToken)));
            Assert.Equal(2, joined.Members.Length);
            Assert.Equal(ownerProfile.World.Region.RegionInstanceId, joined.Region.RegionInstanceId);
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync(
                $"/v1/sessions/{session.SessionId}/invites", new ExpectedRevisionRequest(joined.Revision))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/v1/sessions/join",
                new JoinSessionRequest(guestProfile.Character.CharacterId, invite.InviteToken))).StatusCode);
            Assert.Equal(JsonSerializer.Serialize(guestProfile),
                JsonSerializer.Serialize(await fixture.Client.GetFromJsonAsync<ProfileView>("/v1/me")));
            fixture.Authenticate(owner.AccessToken);
            Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(
                $"/v1/sessions/{session.SessionId}/leave", new ExpectedRevisionRequest(session.Revision))).StatusCode);
            SessionView closed = await Read<SessionView>(await fixture.Client.PostAsJsonAsync(
                $"/v1/sessions/{session.SessionId}/leave", new ExpectedRevisionRequest(joined.Revision)));
            Assert.Equal("closed", closed.State);
            fixture.Authenticate(guest.AccessToken);
            Assert.Equal("closed", (await fixture.Client.GetFromJsonAsync<SessionView>($"/v1/sessions/{session.SessionId}"))!.State);
        }

        [Fact]
        public async Task RestartPreservesWorldButDiscardsAdmissionAndInvitationSecrets()
        {
            await using Fixture fixture = await Fixture.Start();
            PlayerCredential player = fixture.Provision();
            ProfileView profile = await fixture.Create(player, "Persistent");
            SessionView session = await Read<SessionView>(await fixture.Client.PostAsJsonAsync("/v1/sessions",
                new StartSessionRequest(profile.Character!.CharacterId, profile.World!.PersonalWorldId, profile.World.Region.RegionInstanceId)));
            InviteView invite = await Read<InviteView>(await fixture.Client.PostAsJsonAsync(
                $"/v1/sessions/{session.SessionId}/invites", new ExpectedRevisionRequest(session.Revision)));
            await fixture.Restart();
            fixture.Authenticate(player.AccessToken);
            Assert.Equal(JsonSerializer.Serialize(profile), JsonSerializer.Serialize(await fixture.Client.GetFromJsonAsync<ProfileView>("/v1/me")));
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/v1/sessions/{session.SessionId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/v1/sessions/join",
                new JoinSessionRequest(profile.Character.CharacterId, invite.InviteToken))).StatusCode);
        }

        [Fact]
        public async Task MalformedOversizedAndUnknownMutationsAreRejected()
        {
            await using Fixture fixture = await Fixture.Start();
            fixture.Authenticate(fixture.Provision().AccessToken);
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Post("/v1/characters", "{not-json", Guid.NewGuid().ToString())).StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
                (await fixture.Post("/v1/characters", new string('x', 17000), Guid.NewGuid().ToString())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Post("/v1/characters/grant-xp", "{\"xp\":100}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Post("/v1/players", "{}")).StatusCode);
        }

        [Fact]
        public async Task ExplicitLocalFlagIsRequiredAndUnknownOptionsFailClosed()
        {
            Assert.Equal(2, await Program.Main([]));
            Assert.Equal(1, await Program.Main(["--dev-local", "--listen", "0.0.0.0"]));
        }

        private static async Task<T> Read<T>(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        private sealed class Fixture : IAsyncDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "session-rpg-http-" + Guid.NewGuid().ToString("N"));
            private WebApplication app = null!;
            public HttpClient Client { get; private set; } = null!;
            private string Database => Path.Combine(directory, "worlds.db");
            public static async Task<Fixture> Start()
            {
                Fixture fixture = new();
                Directory.CreateDirectory(fixture.directory);
                await fixture.StartApp();
                return fixture;
            }
            private async Task StartApp()
            {
                app = DevelopmentServer.Build(Database, 0);
                await app.StartAsync();
                string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                Assert.Equal("127.0.0.1", new Uri(address).Host);
                Client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
            }
            public PlayerCredential Provision() => new GameStore(Database).ProvisionPlayer(DateTimeOffset.UtcNow);
            public void Authenticate(string token) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            public async Task<ProfileView> Create(PlayerCredential player, string name)
            {
                Authenticate(player.AccessToken);
                return await Read<ProfileView>(await Post("/v1/characters",
                    JsonSerializer.Serialize(new CreateCharacterRequest(name, 1, 1)), Guid.NewGuid().ToString()));
            }
            public Task<HttpResponseMessage> Post(string path, string json, string? key = null)
            {
                HttpRequestMessage request = new(HttpMethod.Post, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                if (key != null) { request.Headers.Add("Idempotency-Key", key); }
                return Client.SendAsync(request);
            }
            public async Task Restart()
            {
                Client.Dispose();
                await app.StopAsync();
                await app.DisposeAsync();
                await StartApp();
            }
            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await app.StopAsync();
                await app.DisposeAsync();
                Directory.Delete(directory, true);
            }
        }
    }
}
