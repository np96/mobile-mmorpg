using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SessionRpg.Networking.Tests
{
    public sealed class PrivateArenaRelayCoordinatorTests
    {
        [Test]
        public async Task Host_SignsInCreatesFourPlayerRelayAndPublishesCode()
        {
            FakeSessionHandle host = HostHandle();
            FakeSessionServices services = new FakeSessionServices
            {
                CreateHandle = host,
            };
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(services);
            List<PrivateArenaSessionState> states = CaptureStates(coordinator);

            Assert.That(await coordinator.HostPrivateArenaAsync(), Is.True);

            Assert.That(services.SignInCalls, Is.EqualTo(1));
            Assert.That(services.CreateCalls, Is.EqualTo(1));
            Assert.That(services.RequestedMaxPlayers,
                Is.EqualTo(PrivateArenaRelayCoordinator.MaximumPlayers));
            Assert.That(coordinator.PlayerId, Is.EqualTo("anonymous-player"));
            Assert.That(coordinator.JoinCode, Is.EqualTo("ABCD12"));
            Assert.That(coordinator.SessionId, Is.EqualTo("session-host"));
            Assert.That(coordinator.IsHost, Is.True);
            Assert.That(states, Is.EqualTo(new[]
            {
                PrivateArenaSessionState.Initializing,
                PrivateArenaSessionState.Creating,
                PrivateArenaSessionState.Host,
            }));
        }

        [Test]
        public async Task Join_TrimsCodeAndPublishesClientState()
        {
            FakeSessionServices services = new FakeSessionServices
            {
                JoinHandle = ClientHandle(),
            };
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(services);
            List<PrivateArenaSessionState> states = CaptureStates(coordinator);

            Assert.That(
                await coordinator.JoinPrivateArenaAsync("  ABCD12  "),
                Is.True);

            Assert.That(services.ReceivedJoinCode, Is.EqualTo("ABCD12"));
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Client));
            Assert.That(coordinator.IsHost, Is.False);
            Assert.That(states, Is.EqualTo(new[]
            {
                PrivateArenaSessionState.Initializing,
                PrivateArenaSessionState.Joining,
                PrivateArenaSessionState.Client,
            }));
        }

        [Test]
        public async Task Join_RejectsMissingCodeBeforeSignIn()
        {
            FakeSessionServices services = new FakeSessionServices();
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(services);

            Assert.That(await coordinator.JoinPrivateArenaAsync("  "), Is.False);

            Assert.That(services.SignInCalls, Is.Zero);
            Assert.That(services.JoinCalls, Is.Zero);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Offline));
            Assert.That(coordinator.LastError, Does.Contain("join code"));
        }

        [Test]
        public async Task Leave_HostDeletesSessionInsteadOfMigratingHost()
        {
            FakeSessionHandle host = HostHandle();
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(new FakeSessionServices
                {
                    CreateHandle = host,
                });
            Assert.That(await coordinator.HostPrivateArenaAsync(), Is.True);

            Assert.That(await coordinator.LeavePrivateArenaAsync(), Is.True);

            Assert.That(host.DeleteCalls, Is.EqualTo(1));
            Assert.That(host.LeaveCalls, Is.Zero);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Offline));
            Assert.That(coordinator.HasActiveSession, Is.False);
            Assert.That(coordinator.JoinCode, Is.Empty);
            Assert.That(coordinator.SessionId, Is.Empty);
        }

        [Test]
        public async Task Leave_ClientLeavesWithoutDeletingSession()
        {
            FakeSessionHandle client = ClientHandle();
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(new FakeSessionServices
                {
                    JoinHandle = client,
                });
            Assert.That(await coordinator.JoinPrivateArenaAsync("ABCD12"), Is.True);

            Assert.That(await coordinator.LeavePrivateArenaAsync(), Is.True);

            Assert.That(client.LeaveCalls, Is.EqualTo(1));
            Assert.That(client.DeleteCalls, Is.Zero);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Offline));
        }

        [TestCase(
            PrivateArenaTerminationReason.HostDeparted,
            "host departed")]
        [TestCase(
            PrivateArenaTerminationReason.SessionDeleted,
            "host closed")]
        [TestCase(
            PrivateArenaTerminationReason.RemovedFromSession,
            "removed")]
        [TestCase(
            PrivateArenaTerminationReason.Disconnected,
            "reconnect is disabled")]
        public async Task RemoteTermination_AbortsWithoutReconnect(
            PrivateArenaTerminationReason reason,
            string expectedError)
        {
            FakeSessionHandle client = ClientHandle();
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(new FakeSessionServices
                {
                    JoinHandle = client,
                });
            Assert.That(await coordinator.JoinPrivateArenaAsync("ABCD12"), Is.True);

            client.RaiseTermination(reason);

            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Aborted));
            Assert.That(coordinator.LastError,
                Does.Contain(expectedError).IgnoreCase);
            Assert.That(coordinator.HasActiveSession, Is.False);
            Assert.That(coordinator.JoinCode, Is.Empty);
            Assert.That(coordinator.SessionId, Is.Empty);
        }

        [Test]
        public async Task ServiceFailure_IsReportedAndCanBeRetried()
        {
            FakeSessionServices services = new FakeSessionServices
            {
                SignInFailure = new InvalidOperationException("authentication unavailable"),
                CreateHandle = HostHandle(),
            };
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(services);

            Assert.That(await coordinator.HostPrivateArenaAsync(), Is.False);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Failed));
            Assert.That(coordinator.LastError,
                Does.Contain("authentication unavailable"));
            Assert.That(coordinator.IsBusy, Is.False);

            services.SignInFailure = null;
            Assert.That(await coordinator.HostPrivateArenaAsync(), Is.True);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Host));
        }

        [Test]
        public async Task ConcurrentStart_IsRejectedWithoutSecondServiceCall()
        {
            TaskCompletionSource<bool> signInGate =
                new TaskCompletionSource<bool>();
            FakeSessionServices services = new FakeSessionServices
            {
                SignInTask = signInGate.Task,
                CreateHandle = HostHandle(),
                JoinHandle = ClientHandle(),
            };
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(services);

            Task<bool> hosting = coordinator.HostPrivateArenaAsync();
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Initializing));
            Assert.That(coordinator.IsBusy, Is.True);

            Assert.That(
                await coordinator.JoinPrivateArenaAsync("ABCD12"),
                Is.False);
            Assert.That(services.JoinCalls, Is.Zero);
            Assert.That(coordinator.LastError,
                Does.Contain("already in progress"));

            signInGate.SetResult(true);
            Assert.That(await hosting, Is.True);
            Assert.That(services.CreateCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedHostDelete_RetainsHandleForRetry()
        {
            FakeSessionHandle host = HostHandle();
            host.DeleteFailuresRemaining = 1;
            PrivateArenaRelayCoordinator coordinator =
                new PrivateArenaRelayCoordinator(new FakeSessionServices
                {
                    CreateHandle = host,
                });
            Assert.That(await coordinator.HostPrivateArenaAsync(), Is.True);

            Assert.That(await coordinator.LeavePrivateArenaAsync(), Is.False);
            Assert.That(coordinator.State,
                Is.EqualTo(PrivateArenaSessionState.Failed));
            Assert.That(coordinator.HasActiveSession, Is.True);

            Assert.That(await coordinator.LeavePrivateArenaAsync(), Is.True);
            Assert.That(host.DeleteCalls, Is.EqualTo(2));
            Assert.That(coordinator.HasActiveSession, Is.False);
        }

        private static List<PrivateArenaSessionState> CaptureStates(
            PrivateArenaRelayCoordinator coordinator)
        {
            List<PrivateArenaSessionState> states =
                new List<PrivateArenaSessionState>();
            coordinator.StateChanged += (state, _) => states.Add(state);
            return states;
        }

        private static FakeSessionHandle HostHandle()
        {
            return new FakeSessionHandle(
                isHost: true,
                joinCode: "ABCD12",
                sessionId: "session-host");
        }

        private static FakeSessionHandle ClientHandle()
        {
            return new FakeSessionHandle(
                isHost: false,
                joinCode: "ABCD12",
                sessionId: "session-client");
        }

        private sealed class FakeSessionServices :
            IPrivateArenaSessionServices
        {
            public string PlayerId => "anonymous-player";

            public Task SignInTask { get; set; } = Task.CompletedTask;

            public Exception SignInFailure { get; set; }

            public FakeSessionHandle CreateHandle { get; set; }

            public FakeSessionHandle JoinHandle { get; set; }

            public int SignInCalls { get; private set; }

            public int CreateCalls { get; private set; }

            public int JoinCalls { get; private set; }

            public int RequestedMaxPlayers { get; private set; }

            public string ReceivedJoinCode { get; private set; }

            public Task EnsureAnonymousSignInAsync()
            {
                SignInCalls++;
                return SignInFailure == null
                    ? SignInTask
                    : Task.FromException(SignInFailure);
            }

            public Task<IPrivateArenaSessionHandle>
                CreatePrivateRelaySessionAsync(int maxPlayers)
            {
                CreateCalls++;
                RequestedMaxPlayers = maxPlayers;
                return Task.FromResult<IPrivateArenaSessionHandle>(CreateHandle);
            }

            public Task<IPrivateArenaSessionHandle>
                JoinPrivateRelaySessionByCodeAsync(string joinCode)
            {
                JoinCalls++;
                ReceivedJoinCode = joinCode;
                return Task.FromResult<IPrivateArenaSessionHandle>(JoinHandle);
            }
        }

        private sealed class FakeSessionHandle :
            IPrivateArenaSessionHandle
        {
            public FakeSessionHandle(
                bool isHost,
                string joinCode,
                string sessionId)
            {
                IsHost = isHost;
                JoinCode = joinCode;
                SessionId = sessionId;
            }

            public event Action<PrivateArenaTerminationReason> Terminated;

            public bool IsHost { get; }

            public string JoinCode { get; }

            public string SessionId { get; }

            public int LeaveCalls { get; private set; }

            public int DeleteCalls { get; private set; }

            public int DeleteFailuresRemaining { get; set; }

            public Task LeaveAsync()
            {
                LeaveCalls++;
                return Task.CompletedTask;
            }

            public Task DeleteAsync()
            {
                DeleteCalls++;
                if (DeleteFailuresRemaining > 0)
                {
                    DeleteFailuresRemaining--;
                    return Task.FromException(new InvalidOperationException(
                        "delete unavailable"));
                }

                return Task.CompletedTask;
            }

            public void RaiseTermination(PrivateArenaTerminationReason reason)
            {
                Terminated?.Invoke(reason);
            }
        }
    }
}
