using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace SessionRpg.Networking
{
    /// <summary>
    /// Unity Gaming Services implementation for invite-code-only Arena sessions.
    /// MPS configures Relay on the authored UnityTransport and starts/stops NGO.
    /// No Lobby, Relay, or NetworkManager lifecycle call is duplicated here.
    /// </summary>
    public sealed class UnityPrivateArenaSessionServices :
        IPrivateArenaSessionServices
    {
        public const string SessionType = "session-rpg-private-arena-v1";

        private readonly NetworkManager _networkManager;
        private readonly UnityTransport _transport;

        public UnityPrivateArenaSessionServices(
            NetworkManager networkManager,
            UnityTransport transport)
        {
            _networkManager = networkManager;
            _transport = transport;
        }

        public string PlayerId
        {
            get
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    return string.Empty;
                }

                return AuthenticationService.Instance.PlayerId ?? string.Empty;
            }
        }

        public async Task EnsureAnonymousSignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public async Task<IPrivateArenaSessionHandle>
            CreatePrivateRelaySessionAsync(int maxPlayers)
        {
            if (maxPlayers != PrivateArenaRelayCoordinator.MaximumPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers),
                    $"Private Arena requires exactly {PrivateArenaRelayCoordinator.MaximumPlayers} slots.");
            }

            PrepareNetworkManager();
            try
            {
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    IsPrivate = true,
                    Name = "Private Arena",
                    Type = SessionType,
                }.WithRelayNetwork();

                IHostSession session = await MultiplayerService.Instance
                    .CreateSessionAsync(options);
                return new UnityPrivateArenaSessionHandle(
                    session,
                    _networkManager);
            }
            catch
            {
                DisableDormantNetworkManager();
                throw;
            }
        }

        public async Task<IPrivateArenaSessionHandle>
            JoinPrivateRelaySessionByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException(
                    "A private Arena join code is required.",
                    nameof(joinCode));
            }

            PrepareNetworkManager();
            try
            {
                ISession session = await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(
                        joinCode.Trim(),
                        new JoinSessionOptions
                        {
                            Type = SessionType,
                        });
                return new UnityPrivateArenaSessionHandle(
                    session,
                    _networkManager);
            }
            catch
            {
                DisableDormantNetworkManager();
                throw;
            }
        }

        private void PrepareNetworkManager()
        {
            if (_networkManager == null || _transport == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager or UnityTransport is not authored.");
            }

            if (_networkManager.IsListening ||
                _networkManager.ShutdownInProgress)
            {
                throw new InvalidOperationException(
                    "An NGO session is already running or shutting down.");
            }

            NetworkManager existing = NetworkManager.Singleton;
            if (existing != null && existing != _networkManager)
            {
                throw new InvalidOperationException(
                    "Another NetworkManager is already active.");
            }

            if (_networkManager.NetworkConfig == null)
            {
                throw new InvalidOperationException(
                    "The authored NetworkManager has no NetworkConfig.");
            }

            // The MPS 2.3.1 GameObjects handler resolves NetworkManager.Singleton
            // and then configures its currently selected UnityTransport with the
            // Relay server data before it calls StartHost/StartClient.
            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.enabled = true;
            if (NetworkManager.Singleton != _networkManager)
            {
                throw new InvalidOperationException(
                    "NGO could not activate the authored NetworkManager.");
            }
        }

        private void DisableDormantNetworkManager()
        {
            if (_networkManager != null &&
                !_networkManager.IsListening &&
                !_networkManager.ShutdownInProgress)
            {
                _networkManager.enabled = false;
            }
        }
    }

    internal sealed class UnityPrivateArenaSessionHandle :
        IPrivateArenaSessionHandle
    {
        private readonly ISession _session;
        private readonly IHostSession _hostSession;
        private readonly NetworkManager _networkManager;
        private readonly bool _isHost;
        private readonly string _originalHostPlayerId;

        private bool _intentionalExit;
        private bool _terminationRaised;
        private bool _eventsDetached;

        public UnityPrivateArenaSessionHandle(
            ISession session,
            NetworkManager networkManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _networkManager = networkManager;
            _isHost = session.IsHost;
            _hostSession = session as IHostSession;
            _originalHostPlayerId = session.Host ?? string.Empty;

            _session.SessionHostChanged += OnSessionHostChanged;
            _session.Deleted += OnSessionDeleted;
            _session.RemovedFromSession += OnRemovedFromSession;
            _session.StateChanged += OnSessionStateChanged;
        }

        public event Action<PrivateArenaTerminationReason> Terminated;

        public bool IsHost => _isHost;

        public string JoinCode => _session.Code ?? string.Empty;

        public string SessionId => _session.Id ?? string.Empty;

        public async Task LeaveAsync()
        {
            _intentionalExit = true;
            try
            {
                await _session.LeaveAsync();
                CompleteIntentionalExit();
            }
            catch
            {
                _intentionalExit = false;
                throw;
            }
        }

        public async Task DeleteAsync()
        {
            if (!_isHost)
            {
                throw new InvalidOperationException(
                    "Only the original Arena host can delete the session.");
            }

            IHostSession host = _hostSession ?? _session.AsHost();
            _intentionalExit = true;
            try
            {
                await host.DeleteAsync();
                CompleteIntentionalExit();
            }
            catch
            {
                _intentionalExit = false;
                throw;
            }
        }

        private void OnSessionHostChanged(string newHostPlayerId)
        {
            if (!string.Equals(
                    newHostPlayerId,
                    _originalHostPlayerId,
                    StringComparison.Ordinal))
            {
                NotifyTermination(PrivateArenaTerminationReason.HostDeparted);
            }
        }

        private void OnSessionDeleted()
        {
            NotifyTermination(PrivateArenaTerminationReason.SessionDeleted);
        }

        private void OnRemovedFromSession()
        {
            NotifyTermination(
                PrivateArenaTerminationReason.RemovedFromSession);
        }

        private void OnSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Deleted)
            {
                NotifyTermination(PrivateArenaTerminationReason.SessionDeleted);
            }
            else if (state == SessionState.Disconnected)
            {
                NotifyTermination(PrivateArenaTerminationReason.Disconnected);
            }
        }

        private void NotifyTermination(PrivateArenaTerminationReason reason)
        {
            if (_intentionalExit || _terminationRaised)
            {
                return;
            }

            _terminationRaised = true;
            DetachEvents();
            Terminated?.Invoke(reason);
            _ = CleanupAfterRemoteTerminationAsync();
        }

        private async Task CleanupAfterRemoteTerminationAsync()
        {
            try
            {
                // This is the MPS-owned shutdown path. Calling
                // NetworkManager.Shutdown directly would bypass the session
                // network module and leave stale MPS state behind.
                await _session.LeaveAsync();
            }
            catch
            {
                // Remote deletion can invalidate the lobby before LeaveAsync
                // completes. MPS still attempts module shutdown before removal.
            }
            finally
            {
                DisableDormantNetworkManager();
            }
        }

        private void CompleteIntentionalExit()
        {
            DetachEvents();
            DisableDormantNetworkManager();
        }

        private void DetachEvents()
        {
            if (_eventsDetached)
            {
                return;
            }

            _eventsDetached = true;
            _session.SessionHostChanged -= OnSessionHostChanged;
            _session.Deleted -= OnSessionDeleted;
            _session.RemovedFromSession -= OnRemovedFromSession;
            _session.StateChanged -= OnSessionStateChanged;
        }

        private void DisableDormantNetworkManager()
        {
            if (_networkManager != null &&
                !_networkManager.IsListening &&
                !_networkManager.ShutdownInProgress)
            {
                _networkManager.enabled = false;
            }
        }
    }
}
