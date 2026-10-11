using System;
using System.Threading.Tasks;

namespace SessionRpg.Networking
{
    public enum PrivateArenaSessionState
    {
        Offline,
        Initializing,
        Creating,
        Joining,
        Host,
        Client,
        Leaving,
        Aborted,
        Failed,
    }

    public enum PrivateArenaTerminationReason
    {
        HostDeparted,
        SessionDeleted,
        RemovedFromSession,
        Disconnected,
    }

    /// <summary>
    /// Test seam around Unity Authentication and Multiplayer Services. The concrete
    /// implementation uses anonymous sign-in and lets an MPS Relay session own NGO
    /// configuration, startup, and shutdown.
    /// </summary>
    public interface IPrivateArenaSessionServices
    {
        string PlayerId { get; }

        Task EnsureAnonymousSignInAsync();

        Task<IPrivateArenaSessionHandle> CreatePrivateRelaySessionAsync(
            int maxPlayers);

        Task<IPrivateArenaSessionHandle> JoinPrivateRelaySessionByCodeAsync(
            string joinCode);
    }

    public interface IPrivateArenaSessionHandle
    {
        event Action<PrivateArenaTerminationReason> Terminated;

        bool IsHost { get; }

        string JoinCode { get; }

        string SessionId { get; }

        Task LeaveAsync();

        Task DeleteAsync();
    }

    /// <summary>
    /// Service-independent private Arena lifecycle. There is deliberately no
    /// reconnect or host-migration path: a host departure aborts the session.
    /// </summary>
    public sealed class PrivateArenaRelayCoordinator
    {
        public const int MaximumPlayers = 4;

        private readonly IPrivateArenaSessionServices _services;
        private IPrivateArenaSessionHandle _session;
        private bool _operationInFlight;

        public PrivateArenaRelayCoordinator(
            IPrivateArenaSessionServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public event Action<PrivateArenaSessionState, string> StateChanged;

        public PrivateArenaSessionState State { get; private set; } =
            PrivateArenaSessionState.Offline;

        public string PlayerId => _services.PlayerId ?? string.Empty;

        public string JoinCode { get; private set; } = string.Empty;

        public string SessionId { get; private set; } = string.Empty;

        public string LastError { get; private set; } = string.Empty;

        public bool HasActiveSession => _session != null;

        public bool IsBusy => _operationInFlight;

        public bool IsHost => _session != null && _session.IsHost;

        public async Task<bool> HostPrivateArenaAsync()
        {
            if (!TryBeginStartOperation())
            {
                return false;
            }

            IPrivateArenaSessionHandle acquired = null;
            try
            {
                SetState(PrivateArenaSessionState.Initializing, string.Empty);
                await _services.EnsureAnonymousSignInAsync();

                SetState(PrivateArenaSessionState.Creating, string.Empty);
                acquired = await _services.CreatePrivateRelaySessionAsync(
                    MaximumPlayers);
                ValidateAcquiredSession(acquired, expectedHost: true);

                Attach(acquired);
                SetState(PrivateArenaSessionState.Host, string.Empty);
                return true;
            }
            catch (Exception exception)
            {
                await CloseFailedAcquisitionAsync(acquired);
                ClearSessionIdentity();
                SetState(
                    PrivateArenaSessionState.Failed,
                    $"Unable to host private Arena: {MessageFor(exception)}");
                return false;
            }
            finally
            {
                _operationInFlight = false;
            }
        }

        public async Task<bool> JoinPrivateArenaAsync(string joinCode)
        {
            string normalizedCode = joinCode?.Trim() ?? string.Empty;
            if (normalizedCode.Length == 0)
            {
                RejectWithoutStateChange("A private Arena join code is required.");
                return false;
            }

            if (!TryBeginStartOperation())
            {
                return false;
            }

            IPrivateArenaSessionHandle acquired = null;
            try
            {
                SetState(PrivateArenaSessionState.Initializing, string.Empty);
                await _services.EnsureAnonymousSignInAsync();

                SetState(PrivateArenaSessionState.Joining, string.Empty);
                acquired = await _services.JoinPrivateRelaySessionByCodeAsync(
                    normalizedCode);
                ValidateAcquiredSession(acquired, expectedHost: false);

                Attach(acquired);
                SetState(PrivateArenaSessionState.Client, string.Empty);
                return true;
            }
            catch (Exception exception)
            {
                await CloseFailedAcquisitionAsync(acquired);
                ClearSessionIdentity();
                SetState(
                    PrivateArenaSessionState.Failed,
                    $"Unable to join private Arena: {MessageFor(exception)}");
                return false;
            }
            finally
            {
                _operationInFlight = false;
            }
        }

        public async Task<bool> LeavePrivateArenaAsync()
        {
            if (_operationInFlight)
            {
                RejectWithoutStateChange(
                    "A private Arena session operation is already in progress.");
                return false;
            }

            if (_session == null)
            {
                ClearSessionIdentity();
                SetState(PrivateArenaSessionState.Offline, string.Empty);
                return true;
            }

            _operationInFlight = true;
            IPrivateArenaSessionHandle leaving = _session;
            Detach(leaving);
            SetState(PrivateArenaSessionState.Leaving, string.Empty);

            try
            {
                if (leaving.IsHost)
                {
                    // Deletion, rather than ordinary host leave, enforces the
                    // Arena rule that host departure aborts every client.
                    await leaving.DeleteAsync();
                }
                else
                {
                    await leaving.LeaveAsync();
                }

                _session = null;
                ClearSessionIdentity();
                SetState(PrivateArenaSessionState.Offline, string.Empty);
                return true;
            }
            catch (Exception exception)
            {
                // Keep the handle so callers can retry a failed service leave.
                _session = leaving;
                leaving.Terminated += OnSessionTerminated;
                SetState(
                    PrivateArenaSessionState.Failed,
                    $"Unable to leave private Arena: {MessageFor(exception)}");
                return false;
            }
            finally
            {
                _operationInFlight = false;
            }
        }

        private bool TryBeginStartOperation()
        {
            if (_operationInFlight)
            {
                RejectWithoutStateChange(
                    "A private Arena session operation is already in progress.");
                return false;
            }

            if (_session != null)
            {
                RejectWithoutStateChange(
                    "Leave the active private Arena before starting another session.");
                return false;
            }

            _operationInFlight = true;
            return true;
        }

        private void Attach(IPrivateArenaSessionHandle session)
        {
            _session = session;
            _session.Terminated += OnSessionTerminated;
            JoinCode = session.JoinCode ?? string.Empty;
            SessionId = session.SessionId ?? string.Empty;
        }

        private void Detach(IPrivateArenaSessionHandle session)
        {
            if (session != null)
            {
                session.Terminated -= OnSessionTerminated;
            }
        }

        private void OnSessionTerminated(PrivateArenaTerminationReason reason)
        {
            IPrivateArenaSessionHandle terminated = _session;
            if (terminated == null)
            {
                return;
            }

            Detach(terminated);
            _session = null;
            ClearSessionIdentity();
            SetState(
                PrivateArenaSessionState.Aborted,
                TerminationMessage(reason));
        }

        private async Task CloseFailedAcquisitionAsync(
            IPrivateArenaSessionHandle acquired)
        {
            if (acquired == null)
            {
                return;
            }

            try
            {
                if (acquired.IsHost)
                {
                    await acquired.DeleteAsync();
                }
                else
                {
                    await acquired.LeaveAsync();
                }
            }
            catch
            {
                // Preserve the original creation/join failure. This cleanup is
                // best-effort because no usable session was published to callers.
            }
        }

        private static void ValidateAcquiredSession(
            IPrivateArenaSessionHandle session,
            bool expectedHost)
        {
            if (session == null)
            {
                throw new InvalidOperationException(
                    "Multiplayer Services returned no session handle.");
            }

            if (session.IsHost != expectedHost)
            {
                throw new InvalidOperationException(
                    expectedHost
                        ? "Created session did not grant host authority."
                        : "Joined session unexpectedly granted host authority.");
            }

            if (string.IsNullOrWhiteSpace(session.SessionId))
            {
                throw new InvalidOperationException(
                    "Multiplayer Services returned a session without an ID.");
            }

            if (expectedHost && string.IsNullOrWhiteSpace(session.JoinCode))
            {
                throw new InvalidOperationException(
                    "Multiplayer Services returned a host session without a join code.");
            }
        }

        private void ClearSessionIdentity()
        {
            JoinCode = string.Empty;
            SessionId = string.Empty;
        }

        private void RejectWithoutStateChange(string error)
        {
            LastError = error ?? string.Empty;
            StateChanged?.Invoke(State, LastError);
        }

        private void SetState(PrivateArenaSessionState state, string error)
        {
            State = state;
            LastError = error ?? string.Empty;
            StateChanged?.Invoke(State, LastError);
        }

        private static string MessageFor(Exception exception)
        {
            if (exception == null)
            {
                return "unknown service failure.";
            }

            Exception root = exception.GetBaseException();
            return string.IsNullOrWhiteSpace(root.Message)
                ? root.GetType().Name
                : root.Message;
        }

        private static string TerminationMessage(
            PrivateArenaTerminationReason reason)
        {
            switch (reason)
            {
                case PrivateArenaTerminationReason.HostDeparted:
                    return "The Arena host departed; this session is aborted.";
                case PrivateArenaTerminationReason.SessionDeleted:
                    return "The Arena host closed the session.";
                case PrivateArenaTerminationReason.RemovedFromSession:
                    return "This player was removed from the Arena session.";
                case PrivateArenaTerminationReason.Disconnected:
                    return "The Arena session disconnected; reconnect is disabled.";
                default:
                    return "The Arena session ended.";
            }
        }
    }
}
