using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SessionRpg.Networking
{
    public enum NetworkSessionState
    {
        Offline,
        Starting,
        Host,
        Client,
        Server,
        Failed,
    }

    /// <summary>
    /// Explicit NGO lifecycle boundary. Gameplay remains offline by default. A
    /// private Arena can use an MPS-managed Relay session, while direct IP remains
    /// available as a local development fallback.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class NetworkSessionController : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;
        [SerializeField] private string _address = "127.0.0.1";
        [SerializeField] private ushort _port = 7777;

        private PrivateArenaRelayCoordinator _privateArenaSession;

        public event Action<NetworkSessionState, string> StateChanged;

        public event Action<PrivateArenaSessionState, string>
            PrivateArenaStateChanged;

        public NetworkSessionState State { get; private set; } = NetworkSessionState.Offline;

        public string LastError { get; private set; } = string.Empty;

        public PrivateArenaSessionState PrivateArenaState =>
            _privateArenaSession?.State ?? PrivateArenaSessionState.Offline;

        public string PrivateArenaJoinCode =>
            _privateArenaSession?.JoinCode ?? string.Empty;

        public string PrivateArenaSessionId =>
            _privateArenaSession?.SessionId ?? string.Empty;

        public string AuthenticatedPlayerId =>
            _privateArenaSession?.PlayerId ?? string.Empty;

        /// <summary>
        /// Pure host-authority state for the active direct-IP host. Transport/RPC
        /// adapters submit network and host-local commands through this same object.
        /// It is intentionally null for client and dedicated-server lifecycle modes.
        /// </summary>
        public HostAuthoritativeSession HostAuthority { get; private set; }

        private void Awake()
        {
            NetworkManager existing = NetworkManager.Singleton;
            if (existing != null && existing != _networkManager)
            {
                if (_networkManager != null)
                {
                    _networkManager.enabled = false;
                }

                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            // NGO makes an enabled NetworkManager persistent from OnEnable, even
            // before a session is started. Keep the authored networking boundary
            // dormant during local play and activate it only for an explicit
            // host/client/server request. This also prevents offline scene reloads
            // from accumulating package teardown callbacks in PlayMode.
            if (_networkManager != null && !_networkManager.IsListening)
            {
                _networkManager.enabled = false;
            }

            if (_networkManager != null && _transport != null)
            {
                _privateArenaSession = new PrivateArenaRelayCoordinator(
                    new UnityPrivateArenaSessionServices(
                        _networkManager,
                        _transport));
                _privateArenaSession.StateChanged += OnPrivateArenaStateChanged;
            }
        }

        private void OnEnable()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback += OnClientConnected;
                _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDisable()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (_privateArenaSession != null)
            {
                _privateArenaSession.StateChanged -= OnPrivateArenaStateChanged;
            }
        }

        public async Task<bool> HostPrivateArenaAsync()
        {
            if (!EnsurePrivateArenaAvailable())
            {
                return false;
            }

            HostAuthority = null;
            bool started = await _privateArenaSession.HostPrivateArenaAsync();
            if (!started)
            {
                SetState(NetworkSessionState.Failed, _privateArenaSession.LastError);
                return false;
            }

            HostAuthority = new HostAuthoritativeSession(
                _networkManager.LocalClientId);
            SetState(NetworkSessionState.Host, string.Empty);
            return true;
        }

        public async Task<bool> JoinPrivateArenaAsync(string joinCode)
        {
            if (!EnsurePrivateArenaAvailable())
            {
                return false;
            }

            HostAuthority = null;
            bool started = await _privateArenaSession.JoinPrivateArenaAsync(joinCode);
            if (!started)
            {
                SetState(NetworkSessionState.Failed, _privateArenaSession.LastError);
                return false;
            }

            SetState(NetworkSessionState.Client, string.Empty);
            return true;
        }

        public async Task<bool> LeavePrivateArenaAsync()
        {
            if (_privateArenaSession == null)
            {
                SetState(
                    NetworkSessionState.Failed,
                    "Private Arena services are not authored.");
                return false;
            }

            AbortHostAuthority();
            bool stopped = await _privateArenaSession.LeavePrivateArenaAsync();
            if (!stopped)
            {
                SetState(NetworkSessionState.Failed, _privateArenaSession.LastError);
            }

            return stopped;
        }

        public bool StartHost()
        {
            bool started = StartSession(
                NetworkSessionState.Host,
                manager => manager.StartHost());
            if (started)
            {
                HostAuthority = new HostAuthoritativeSession(_networkManager.LocalClientId);
            }

            return started;
        }

        public bool StartClient(string address = null)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                _address = address.Trim();
            }

            HostAuthority = null;
            return StartSession(NetworkSessionState.Client, manager => manager.StartClient());
        }

        public bool StartServer()
        {
            HostAuthority = null;
            return StartSession(NetworkSessionState.Server, manager => manager.StartServer());
        }

        public bool BeginHostAuthoritativeSession()
        {
            return State == NetworkSessionState.Host &&
                   HostAuthority != null &&
                   HostAuthority.BeginSession();
        }

        public bool EnterHostAuthoritativeResults()
        {
            return State == NetworkSessionState.Host &&
                   HostAuthority != null &&
                   HostAuthority.EnterResults();
        }

        public bool StartHostAuthoritativeReplay()
        {
            return State == NetworkSessionState.Host &&
                   HostAuthority != null &&
                   HostAuthority.StartReplay();
        }

        public void Shutdown()
        {
            if (_privateArenaSession != null &&
                _privateArenaSession.State != PrivateArenaSessionState.Offline)
            {
                // MPS owns its NGO lifecycle. Its Leave/Delete path must be used
                // instead of calling NetworkManager.Shutdown directly.
                _ = ShutdownAsync();
                return;
            }

            ShutdownDirectIp();
        }

        public async Task<bool> ShutdownAsync()
        {
            if (_privateArenaSession != null &&
                _privateArenaSession.State != PrivateArenaSessionState.Offline)
            {
                return await LeavePrivateArenaAsync();
            }

            ShutdownDirectIp();
            return true;
        }

        private bool StartSession(
            NetworkSessionState targetState,
            Func<NetworkManager, bool> start)
        {
            if (_privateArenaSession != null &&
                _privateArenaSession.State != PrivateArenaSessionState.Offline)
            {
                SetState(
                    NetworkSessionState.Failed,
                    "Leave or reset the private Arena session before using direct IP.");
                return false;
            }

            if (_networkManager == null || _transport == null)
            {
                SetState(NetworkSessionState.Failed, "NetworkManager or UnityTransport is not authored.");
                return false;
            }

            if (_networkManager.IsListening)
            {
                SetState(NetworkSessionState.Failed, "A network session is already running.");
                return false;
            }

            NetworkManager existing = NetworkManager.Singleton;
            if (existing != null && existing != _networkManager)
            {
                SetState(NetworkSessionState.Failed, "Another NetworkManager is already active.");
                return false;
            }

            _networkManager.enabled = true;
            if (NetworkManager.Singleton != _networkManager)
            {
                SetState(NetworkSessionState.Failed, "NGO could not activate the authored NetworkManager.");
                return false;
            }

            _transport.SetConnectionData(_address, _port);
            SetState(NetworkSessionState.Starting, string.Empty);
            if (!start(_networkManager))
            {
                SetState(NetworkSessionState.Failed, "NGO could not start the requested session.");
                return false;
            }

            SetState(targetState, string.Empty);
            return true;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (HostAuthority != null &&
                clientId != HostAuthority.Room.HostClientId)
            {
                RoomJoinResult result = HostAuthority.TryJoinClient(
                    clientId,
                    out _);
                if (result != RoomJoinResult.Joined &&
                    _networkManager != null &&
                    _networkManager.IsServer)
                {
                    _networkManager.DisconnectClient(
                        clientId,
                        $"Authoritative room admission rejected: {result}.");
                    return;
                }
            }

            if (_networkManager != null && clientId == _networkManager.LocalClientId)
            {
                StateChanged?.Invoke(State, string.Empty);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (HostAuthority != null)
            {
                HostAuthority.HandleDisconnect(clientId);
            }

            if (_networkManager == null || clientId != _networkManager.LocalClientId)
            {
                return;
            }

            if (_privateArenaSession != null &&
                _privateArenaSession.State != PrivateArenaSessionState.Offline)
            {
                // The MPS session will publish its own disconnected/deleted/host
                // departure state and complete the NGO shutdown it owns.
                return;
            }

            string reason = _networkManager.DisconnectReason;
            SetState(NetworkSessionState.Offline, reason);
        }

        private bool EnsurePrivateArenaAvailable()
        {
            if (_privateArenaSession != null)
            {
                return true;
            }

            SetState(
                NetworkSessionState.Failed,
                "NetworkManager or UnityTransport is not authored for private Arena.");
            return false;
        }

        private void OnPrivateArenaStateChanged(
            PrivateArenaSessionState state,
            string error)
        {
            PrivateArenaStateChanged?.Invoke(state, error ?? string.Empty);

            switch (state)
            {
                case PrivateArenaSessionState.Initializing:
                case PrivateArenaSessionState.Creating:
                case PrivateArenaSessionState.Joining:
                case PrivateArenaSessionState.Leaving:
                    SetState(NetworkSessionState.Starting, error);
                    break;
                case PrivateArenaSessionState.Aborted:
                    AbortHostAuthority();
                    SetState(NetworkSessionState.Failed, error);
                    break;
                case PrivateArenaSessionState.Failed:
                    SetState(NetworkSessionState.Failed, error);
                    break;
                case PrivateArenaSessionState.Offline:
                    HostAuthority = null;
                    SetState(NetworkSessionState.Offline, error);
                    break;
            }
        }

        private void AbortHostAuthority()
        {
            if (HostAuthority != null &&
                HostAuthority.Room.Phase != AuthoritativeRoomPhase.Aborted)
            {
                HostAuthority.HandleDisconnect(HostAuthority.Room.HostClientId);
            }

            HostAuthority = null;
        }

        private void ShutdownDirectIp()
        {
            AbortHostAuthority();
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            SetState(NetworkSessionState.Offline, string.Empty);
        }

        private void SetState(NetworkSessionState state, string error)
        {
            State = state;
            LastError = error ?? string.Empty;
            StateChanged?.Invoke(State, LastError);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _port = _port == 0 ? (ushort)7777 : _port;
            _address = string.IsNullOrWhiteSpace(_address) ? "127.0.0.1" : _address.Trim();
        }
#endif
    }
}
