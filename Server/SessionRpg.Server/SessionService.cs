using System.Security.Cryptography;
using System.Text;

namespace SessionRpg.Server
{
    // Admission metadata only. No combat, transport, host migration, or rewards.
    // A single lock makes revision checks, invite consumption, and membership atomic.
    public sealed class SessionService
    {
        private readonly GameStore store;
        private readonly TimeProvider clock;
        private readonly object gate = new();
        private readonly Dictionary<Guid, SessionView> sessions = new();
        private readonly Dictionary<string, PendingInvite> invites = new();
        private const int Capacity = 128;
        private sealed record PendingInvite(Guid SessionId, DateTimeOffset ExpiresAt);

        public SessionService(GameStore store, TimeProvider clock)
        {
            this.store = store;
            this.clock = clock;
        }

        public SessionView Start(Guid playerId, StartSessionRequest request)
        {
            RequireCharacter(playerId, request.CharacterId);
            WorldView world = store.GetOwnedWorld(playerId, request.PersonalWorldId);
            if (request.RegionInstanceId != world.Region.RegionInstanceId)
            {
                throw new ApiFault(404, "region_not_found");
            }
            lock (gate)
            {
                Prune();
                SessionView? existing = ActiveSession(playerId);
                if (existing != null)
                {
                    if (existing.OwnerPlayerId == playerId &&
                        existing.PersonalWorldId == request.PersonalWorldId &&
                        existing.RegionInstanceId == request.RegionInstanceId &&
                        existing.Members[0].CharacterId == request.CharacterId)
                    {
                        return Copy(existing);
                    }
                    throw new ApiFault(409, "already_in_session");
                }
                if (sessions.Count >= Capacity)
                {
                    // Closed membership is a best-effort tombstone, never a way
                    // to consume all capacity while no one is actually playing.
                    Guid? closed = sessions.Values.FirstOrDefault(value => value.State == "closed")?.SessionId;
                    if (closed.HasValue) { sessions.Remove(closed.Value); }
                    else { throw new ApiFault(503, "session_capacity_reached"); }
                }
                SessionView session = new(Guid.NewGuid(), playerId, world.PersonalWorldId,
                    request.RegionInstanceId, "lobby", 1, clock.GetUtcNow().AddHours(1),
                    [new SessionMember(playerId, request.CharacterId)], world.Region);
                sessions.Add(session.SessionId, session);
                return Copy(session);
            }
        }

        public SessionView Get(Guid playerId, Guid sessionId)
        {
            lock (gate)
            {
                Prune();
                return Copy(RequireMember(playerId, sessionId));
            }
        }

        public InviteView CreateInvite(Guid playerId, Guid sessionId, long expectedRevision)
        {
            lock (gate)
            {
                Prune();
                SessionView session = RequireMember(playerId, sessionId);
                if (session.OwnerPlayerId != playerId)
                {
                    throw new ApiFault(403, "owner_required");
                }
                RequireOpenRevision(session, expectedRevision);
                if (session.Members.Length >= 2)
                {
                    throw new ApiFault(409, "session_full");
                }
                RevokeInvites(sessionId);
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                DateTimeOffset expiry = clock.GetUtcNow().AddMinutes(5);
                if (expiry > session.ExpiresAt) { expiry = session.ExpiresAt; }
                invites.Add(Hash(token), new PendingInvite(sessionId, expiry));
                session = session with { Revision = session.Revision + 1 };
                sessions[sessionId] = session;
                return new InviteView(sessionId, token, expiry, session.Revision);
            }
        }

        public SessionView Join(Guid playerId, JoinSessionRequest request)
        {
            RequireCharacter(playerId, request.CharacterId);
            if (string.IsNullOrEmpty(request.InviteToken) || request.InviteToken.Length != 64)
            {
                throw new ApiFault(404, "invite_not_found");
            }
            lock (gate)
            {
                Prune();
                string hash = Hash(request.InviteToken);
                if (!invites.TryGetValue(hash, out PendingInvite? invite) ||
                    !sessions.TryGetValue(invite.SessionId, out SessionView? session))
                {
                    throw new ApiFault(404, "invite_not_found");
                }
                if (session.State != "lobby" || session.Members.Length >= 2)
                {
                    throw new ApiFault(409, "session_unavailable");
                }
                if (ActiveSession(playerId) != null)
                {
                    throw new ApiFault(409, "already_in_session");
                }
                session = session with
                {
                    Members = [.. session.Members, new SessionMember(playerId, request.CharacterId)],
                    Revision = session.Revision + 1
                };
                sessions[session.SessionId] = session;
                RevokeInvites(session.SessionId);
                return Copy(session);
            }
        }

        public SessionView Leave(Guid playerId, Guid sessionId, long expectedRevision)
        {
            lock (gate)
            {
                Prune();
                SessionView session = RequireMember(playerId, sessionId);
                RequireOpenRevision(session, expectedRevision);
                session = playerId == session.OwnerPlayerId
                    ? session with { State = "closed", Revision = session.Revision + 1 }
                    : session with
                    {
                        Members = session.Members.Where(member => member.PlayerId != playerId).ToArray(),
                        Revision = session.Revision + 1
                    };
                sessions[sessionId] = session;
                RevokeInvites(sessionId);
                return Copy(session);
            }
        }

        private void RequireCharacter(Guid playerId, Guid characterId)
        {
            if (store.GetProfile(playerId).Character?.CharacterId != characterId)
            {
                throw new ApiFault(404, "character_not_found");
            }
        }

        private SessionView RequireMember(Guid playerId, Guid sessionId)
        {
            if (!sessions.TryGetValue(sessionId, out SessionView? session) ||
                !session.Members.Any(member => member.PlayerId == playerId))
            {
                throw new ApiFault(404, "session_not_found");
            }
            return session;
        }

        private static void RequireOpenRevision(SessionView session, long revision)
        {
            if (session.State != "lobby") { throw new ApiFault(409, "session_closed"); }
            if (session.Revision != revision) { throw new ApiFault(409, "revision_conflict"); }
        }

        private SessionView? ActiveSession(Guid playerId) => sessions.Values.FirstOrDefault(
            session => session.State == "lobby" && session.Members.Any(member => member.PlayerId == playerId));

        private void Prune()
        {
            DateTimeOffset now = clock.GetUtcNow();
            foreach (Guid id in sessions.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray())
            {
                sessions.Remove(id);
            }
            foreach (string key in invites.Where(pair => pair.Value.ExpiresAt <= now ||
                !sessions.ContainsKey(pair.Value.SessionId)).Select(pair => pair.Key).ToArray())
            {
                invites.Remove(key);
            }
        }

        private void RevokeInvites(Guid sessionId)
        {
            foreach (string key in invites.Where(pair => pair.Value.SessionId == sessionId)
                .Select(pair => pair.Key).ToArray())
            {
                invites.Remove(key);
            }
        }

        private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static SessionView Copy(SessionView session) => session with
        {
            Members = [.. session.Members],
            Region = session.Region with { Nodes = [.. session.Region.Nodes], Edges = [.. session.Region.Edges] }
        };
    }
}
