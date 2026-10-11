using SessionRpg.Server;
using Xunit;

namespace SessionRpg.Server.Tests;

public sealed class SessionServiceTests : IDisposable
{
    private readonly string directory;
    private readonly string databasePath;
    private readonly AdjustableTimeProvider clock = new();
    private GameStore store;
    private SessionService service;

    public SessionServiceTests()
    {
        directory = Path.Combine(Path.GetTempPath(), "session-rpg-session-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        databasePath = Path.Combine(directory, "game.db");
        store = new GameStore(databasePath);
        service = new SessionService(store, clock);
    }

    [Fact]
    public void OwnerCanStartTheirSavedWorldWithTheirCharacter()
    {
        PlayerAccount owner = Register("Owner");

        SessionView session = Start(owner);

        Assert.NotEqual(Guid.Empty, session.SessionId);
        Assert.Equal(owner.PlayerId, session.OwnerPlayerId);
        Assert.Equal(owner.World.PersonalWorldId, session.PersonalWorldId);
        Assert.Equal(owner.World.Region.RegionInstanceId, session.RegionInstanceId);
        Assert.Equal("lobby", session.State);
        Assert.Equal(1, session.Revision);
        Assert.Equal(clock.GetUtcNow().AddHours(1), session.ExpiresAt);
        Assert.Equal(new SessionMember(owner.PlayerId, owner.Character.CharacterId), Assert.Single(session.Members));
        AssertRegionEqual(owner.World.Region, session.Region);
    }

    [Fact]
    public void StartRejectsForeignWorldWithoutDisclosingItsExistence()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount other = Register("Other");

        AssertFault(404, "world_not_found", () => service.Start(other.PlayerId,
            new StartSessionRequest(other.Character.CharacterId, owner.World.PersonalWorldId,
                owner.World.Region.RegionInstanceId)));
        AssertFault(404, "world_not_found", () => service.Start(other.PlayerId,
            new StartSessionRequest(other.Character.CharacterId, Guid.NewGuid(), Guid.NewGuid())));

        Assert.Single(Start(other).Members);
    }

    [Fact]
    public void StartRejectsForeignCharacterAndUnknownRegion()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount other = Register("Other");

        AssertFault(404, "character_not_found", () => service.Start(owner.PlayerId,
            new StartSessionRequest(other.Character.CharacterId, owner.World.PersonalWorldId,
                owner.World.Region.RegionInstanceId)));
        AssertFault(404, "region_not_found", () => service.Start(owner.PlayerId,
            new StartSessionRequest(owner.Character.CharacterId, owner.World.PersonalWorldId,
                Guid.NewGuid())));

        Assert.Single(Start(owner).Members);
    }

    [Fact]
    public void RepeatedStartReturnsTheSameSessionAndItsCurrentRevision()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest", 3, 2);
        SessionView first = Start(owner);

        SessionView repeated = Start(owner);
        Assert.Equal(first.SessionId, repeated.SessionId);
        Assert.Equal(first.Revision, repeated.Revision);
        Assert.Equal(first.ExpiresAt, repeated.ExpiresAt);

        InviteView invite = Invite(owner, first);
        SessionView joined = Join(guest, invite);
        SessionView afterJoin = Start(owner);
        Assert.Equal(first.SessionId, afterJoin.SessionId);
        Assert.Equal(joined.Revision, afterJoin.Revision);
        Assert.Equal(joined.Members, afterJoin.Members);
        Assert.Equal(first.ExpiresAt, afterJoin.ExpiresAt);
    }

    [Fact]
    public void GuestJoinsOwnersRegionWithoutChangingEitherPersistentProfile()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest", 3, 2);
        SessionView session = Start(owner);
        InviteView invite = Invite(owner, session);

        SessionView joined = Join(guest, invite);

        Assert.Equal(session.SessionId, joined.SessionId);
        Assert.Equal(owner.World.PersonalWorldId, joined.PersonalWorldId);
        Assert.Equal(owner.World.Region.RegionInstanceId, joined.RegionInstanceId);
        Assert.Equal(invite.Revision + 1, joined.Revision);
        Assert.Equal(2, joined.Members.Length);
        Assert.Contains(new SessionMember(guest.PlayerId, guest.Character.CharacterId), joined.Members);
        AssertRegionEqual(owner.World.Region, joined.Region);
        Assert.Equal(owner.Character, store.GetProfile(owner.PlayerId).Character);
        Assert.Equal(guest.Character, store.GetProfile(guest.PlayerId).Character);
        AssertWorldEqual(owner.World, store.GetOwnedWorld(owner.PlayerId, owner.World.PersonalWorldId));
        AssertWorldEqual(guest.World, store.GetOwnedWorld(guest.PlayerId, guest.World.PersonalWorldId));
    }

    [Fact]
    public void ForeignCharacterCannotRedeemAnOtherwiseValidInvite()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        PlayerAccount other = Register("Other");
        InviteView invite = Invite(owner, Start(owner));

        AssertFault(404, "character_not_found", () => service.Join(other.PlayerId,
            new JoinSessionRequest(guest.Character.CharacterId, invite.InviteToken)));

        Assert.Equal(2, Join(guest, invite).Members.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-invite")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public void MissingOrUnknownInviteCannotAdmitAPlayer(string token)
    {
        PlayerAccount guest = Register("Guest");

        AssertFault(404, "invite_not_found", () => service.Join(guest.PlayerId,
            new JoinSessionRequest(guest.Character.CharacterId, token)));
    }

    [Fact]
    public void ConsumedInviteCannotBeUsedAgainByTheSameOrAnotherGuest()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        PlayerAccount other = Register("Other");
        InviteView invite = Invite(owner, Start(owner));
        SessionView joined = Join(guest, invite);

        AssertFault(404, "invite_not_found", () => Join(guest, invite));
        AssertFault(404, "invite_not_found", () => Join(other, invite));

        SessionView current = service.Get(owner.PlayerId, joined.SessionId);
        Assert.Equal(joined.Revision, current.Revision);
        Assert.Equal(joined.Members, current.Members);
    }

    [Fact]
    public void IssuingANewInviteRevokesThePreviousToken()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView session = Start(owner);
        InviteView original = Invite(owner, session);

        InviteView replacement = service.CreateInvite(owner.PlayerId, session.SessionId, original.Revision);

        Assert.NotEqual(original.InviteToken, replacement.InviteToken);
        Assert.Equal(original.Revision + 1, replacement.Revision);
        AssertFault(404, "invite_not_found", () => Join(guest, original));
        Assert.Equal(2, Join(guest, replacement).Members.Length);
    }

    [Fact]
    public void InviteExpiresAtItsExactDeadlineWithoutClosingTheSession()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView session = Start(owner);
        InviteView invite = Invite(owner, session);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), invite.ExpiresAt);

        clock.AdvanceTo(invite.ExpiresAt);

        AssertFault(404, "invite_not_found", () => Join(guest, invite));
        SessionView current = service.Get(owner.PlayerId, session.SessionId);
        Assert.Equal("lobby", current.State);
        Assert.Equal(invite.Revision, current.Revision);
        Assert.Equal(2, Join(guest, Invite(owner, current)).Members.Length);
    }

    [Fact]
    public void OnlyOwnerCanInviteAndAFullSessionRejectsAnotherInvite()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView joined = Join(guest, Invite(owner, Start(owner)));

        AssertFault(403, "owner_required", () => service.CreateInvite(guest.PlayerId,
            joined.SessionId, joined.Revision));
        AssertFault(409, "session_full", () => service.CreateInvite(owner.PlayerId,
            joined.SessionId, joined.Revision));

        Assert.Equal(joined.Revision, service.Get(owner.PlayerId, joined.SessionId).Revision);
    }

    [Fact]
    public void VisitorCanReadOnlyTheSessionTheyActuallyJoined()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        PlayerAccount other = Register("Other");
        SessionView session = Start(owner);
        SessionView unrelated = Start(other);

        AssertFault(404, "session_not_found", () => service.Get(guest.PlayerId, session.SessionId));
        AssertFault(404, "session_not_found", () => service.CreateInvite(guest.PlayerId,
            session.SessionId, session.Revision));
        AssertFault(404, "session_not_found", () => service.Leave(guest.PlayerId,
            session.SessionId, session.Revision));

        SessionView joined = Join(guest, Invite(owner, session));
        Assert.Equal(joined.SessionId, service.Get(guest.PlayerId, joined.SessionId).SessionId);
        AssertFault(404, "session_not_found", () => service.Get(guest.PlayerId, unrelated.SessionId));
        AssertFault(404, "session_not_found", () => service.Get(other.PlayerId, joined.SessionId));
    }

    [Fact]
    public void StaleRevisionsCannotIssueInvitesOrRemoveMembers()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView session = Start(owner);
        InviteView invite = Invite(owner, session);

        AssertFault(409, "revision_conflict", () => service.CreateInvite(owner.PlayerId,
            session.SessionId, session.Revision));

        SessionView joined = Join(guest, invite);
        AssertFault(409, "revision_conflict", () => service.Leave(owner.PlayerId,
            joined.SessionId, invite.Revision));
        AssertFault(409, "revision_conflict", () => service.Leave(guest.PlayerId,
            joined.SessionId, invite.Revision));

        SessionView current = service.Get(owner.PlayerId, joined.SessionId);
        Assert.Equal("lobby", current.State);
        Assert.Equal(joined.Revision, current.Revision);
        Assert.Equal(joined.Members, current.Members);
    }

    [Fact]
    public async Task SimultaneousRedemptionAdmitsExactlyOneOfTwoGuests()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount first = Register("First");
        PlayerAccount second = Register("Second", 3, 2);
        InviteView invite = Invite(owner, Start(owner));
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<AdmissionResult> Attempt(PlayerAccount guest)
        {
            await release.Task;
            try { return new AdmissionResult(Join(guest, invite), null); }
            catch (ApiFault fault) { return new AdmissionResult(null, fault); }
        }

        Task<AdmissionResult> firstAttempt = Task.Run(() => Attempt(first));
        Task<AdmissionResult> secondAttempt = Task.Run(() => Attempt(second));
        release.SetResult();
        AdmissionResult[] results = await Task.WhenAll(firstAttempt, secondAttempt);

        AdmissionResult accepted = Assert.Single(results, result => result.Session is not null);
        AdmissionResult rejected = Assert.Single(results, result => result.Fault is not null);
        Assert.Equal(404, rejected.Fault!.StatusCode);
        Assert.Equal("invite_not_found", rejected.Fault.Code);
        SessionView current = service.Get(owner.PlayerId, invite.SessionId);
        Assert.Equal(2, current.Members.Length);
        Assert.Equal(invite.Revision + 1, current.Revision);
        Assert.Equal(accepted.Session!.Members, current.Members);
    }

    [Fact]
    public void OwnerLeaveClosesTheSessionButBothFormerMembersCanReadIt()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView joined = Join(guest, Invite(owner, Start(owner)));

        SessionView closed = service.Leave(owner.PlayerId, joined.SessionId, joined.Revision);

        Assert.Equal("closed", closed.State);
        Assert.Equal(joined.Revision + 1, closed.Revision);
        Assert.Equal(joined.Members, closed.Members);
        Assert.Equal("closed", service.Get(owner.PlayerId, closed.SessionId).State);
        Assert.Equal("closed", service.Get(guest.PlayerId, closed.SessionId).State);
        AssertFault(409, "session_closed", () => service.CreateInvite(owner.PlayerId,
            closed.SessionId, closed.Revision));
        AssertFault(409, "session_closed", () => service.Leave(guest.PlayerId,
            closed.SessionId, closed.Revision));

        Assert.NotEqual(closed.SessionId, Start(owner).SessionId);
        Assert.NotEqual(closed.SessionId, Start(guest).SessionId);
    }

    [Fact]
    public void OwnerLeaveRevokesAnUnusedInvite()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView session = Start(owner);
        InviteView invite = Invite(owner, session);

        service.Leave(owner.PlayerId, session.SessionId, invite.Revision);

        AssertFault(404, "invite_not_found", () => Join(guest, invite));
        Assert.Single(Start(guest).Members);
    }

    [Fact]
    public void GuestCanLeaveAndReturnOnlyWithANewOwnerInvite()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        InviteView firstInvite = Invite(owner, Start(owner));
        SessionView joined = Join(guest, firstInvite);

        SessionView afterLeave = service.Leave(guest.PlayerId, joined.SessionId, joined.Revision);

        Assert.Equal("lobby", afterLeave.State);
        Assert.Equal(joined.Revision + 1, afterLeave.Revision);
        Assert.Equal(owner.PlayerId, Assert.Single(afterLeave.Members).PlayerId);
        AssertFault(404, "session_not_found", () => service.Get(guest.PlayerId, joined.SessionId));
        AssertFault(404, "invite_not_found", () => Join(guest, firstInvite));

        InviteView newInvite = Invite(owner, afterLeave);
        SessionView rejoined = Join(guest, newInvite);
        Assert.Equal(joined.SessionId, rejoined.SessionId);
        Assert.Equal(newInvite.Revision + 1, rejoined.Revision);
        Assert.Equal(2, rejoined.Members.Length);
    }

    [Fact]
    public void OwnerCannotJoinAnotherSessionUntilTheirOwnSessionIsClosed()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount visitor = Register("Visitor");
        SessionView visitorsSession = Start(visitor);
        InviteView invite = Invite(owner, Start(owner));

        AssertFault(409, "already_in_session", () => Join(visitor, invite));

        service.Leave(visitor.PlayerId, visitorsSession.SessionId, visitorsSession.Revision);
        Assert.Equal(2, Join(visitor, invite).Members.Length);
    }

    [Fact]
    public void JoinedGuestCannotStartOrJoinASecondActiveSession()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        PlayerAccount other = Register("Other");
        PlayerAccount replacement = Register("Replacement");
        SessionView joined = Join(guest, Invite(owner, Start(owner)));
        InviteView otherInvite = Invite(other, Start(other));

        AssertFault(409, "already_in_session", () => Start(guest));
        AssertFault(409, "already_in_session", () => Join(guest, otherInvite));

        Assert.Equal(joined.SessionId, service.Get(guest.PlayerId, joined.SessionId).SessionId);
        Assert.Equal(2, Join(replacement, otherInvite).Members.Length);
    }

    [Fact]
    public void RestartDropsSessionsAndInvitesButPreservesBothPlayersSavedData()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest", 3, 2);
        PlayerAccount other = Register("Other");
        SessionView joined = Join(guest, Invite(owner, Start(owner)));
        InviteView unusedInvite = Invite(other, Start(other));

        DisposeStore();
        store = new GameStore(databasePath);
        service = new SessionService(store, clock);

        AssertFault(404, "session_not_found", () => service.Get(owner.PlayerId, joined.SessionId));
        AssertFault(404, "session_not_found", () => service.Get(guest.PlayerId, joined.SessionId));
        AssertFault(404, "invite_not_found", () => Join(guest, unusedInvite));
        Assert.Equal(owner.Character, store.GetProfile(owner.PlayerId).Character);
        Assert.Equal(guest.Character, store.GetProfile(guest.PlayerId).Character);
        AssertWorldEqual(owner.World, store.GetOwnedWorld(owner.PlayerId, owner.World.PersonalWorldId));
        AssertWorldEqual(guest.World, store.GetOwnedWorld(guest.PlayerId, guest.World.PersonalWorldId));
        Assert.NotEqual(joined.SessionId, Start(owner).SessionId);
        Assert.NotEqual(joined.SessionId, Start(guest).SessionId);
    }

    [Fact]
    public void ReturnedMemberAndRegionArraysCannotMutateStoredSessionState()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView started = Start(owner);
        Guid sessionId = started.SessionId;
        CorruptArrays(started);

        SessionView read = service.Get(owner.PlayerId, sessionId);
        Assert.Equal(owner.PlayerId, Assert.Single(read.Members).PlayerId);
        AssertRegionEqual(owner.World.Region, read.Region);
        CorruptArrays(read);

        SessionView clean = service.Get(owner.PlayerId, sessionId);
        Assert.Equal(owner.PlayerId, Assert.Single(clean.Members).PlayerId);
        AssertRegionEqual(owner.World.Region, clean.Region);
        SessionView joined = Join(guest, Invite(owner, clean));
        long joinedRevision = joined.Revision;
        CorruptArrays(joined);

        SessionView afterJoin = service.Get(guest.PlayerId, sessionId);
        Assert.Equal(owner.PlayerId, afterJoin.Members[0].PlayerId);
        Assert.Equal(guest.PlayerId, afterJoin.Members[1].PlayerId);
        AssertRegionEqual(owner.World.Region, afterJoin.Region);
        SessionView left = service.Leave(guest.PlayerId, sessionId, joinedRevision);
        CorruptArrays(left);

        SessionView afterLeave = service.Get(owner.PlayerId, sessionId);
        Assert.Equal(owner.PlayerId, Assert.Single(afterLeave.Members).PlayerId);
        AssertRegionEqual(owner.World.Region, afterLeave.Region);
        AssertWorldEqual(owner.World, store.GetOwnedWorld(owner.PlayerId, owner.World.PersonalWorldId));
    }

    [Fact]
    public void SessionExpiryRemovesMembershipAndAllowsANewSessionAtTheDeadline()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView joined = Join(guest, Invite(owner, Start(owner)));

        clock.AdvanceTo(joined.ExpiresAt);

        AssertFault(404, "session_not_found", () => service.Get(owner.PlayerId, joined.SessionId));
        AssertFault(404, "session_not_found", () => service.Get(guest.PlayerId, joined.SessionId));
        AssertFault(404, "session_not_found", () => service.Leave(owner.PlayerId,
            joined.SessionId, joined.Revision));
        SessionView restarted = Start(owner);
        Assert.NotEqual(joined.SessionId, restarted.SessionId);
        Assert.Equal(clock.GetUtcNow().AddHours(1), restarted.ExpiresAt);
        Assert.Single(Start(guest).Members);
    }

    [Fact]
    public void InviteDeadlineCannotOutliveItsSession()
    {
        PlayerAccount owner = Register("Owner");
        PlayerAccount guest = Register("Guest");
        SessionView session = Start(owner);
        clock.AdvanceTo(session.ExpiresAt.AddMinutes(-2));

        InviteView invite = Invite(owner, session);
        Assert.Equal(session.ExpiresAt, invite.ExpiresAt);
        clock.AdvanceTo(session.ExpiresAt);

        AssertFault(404, "invite_not_found", () => Join(guest, invite));
        AssertFault(404, "session_not_found", () => service.Get(owner.PlayerId, session.SessionId));
    }

    [Fact]
    public void RepeatedStartAndCloseReclaimsClosedSessionsAndKeepsHistoryBounded()
    {
        PlayerAccount owner = Register("Owner");
        List<Guid> closedSessionIds = [];

        for (int index = 0; index < 130; index++)
        {
            SessionView started = Start(owner);
            Assert.DoesNotContain(started.SessionId, closedSessionIds);
            SessionView closed = service.Leave(owner.PlayerId, started.SessionId, started.Revision);
            Assert.Equal("closed", closed.State);
            closedSessionIds.Add(closed.SessionId);
        }

        int readableCount = 0;
        foreach (Guid sessionId in closedSessionIds)
        {
            try
            {
                Assert.Equal("closed", service.Get(owner.PlayerId, sessionId).State);
                readableCount++;
            }
            catch (ApiFault fault)
            {
                Assert.Equal(404, fault.StatusCode);
                Assert.Equal("session_not_found", fault.Code);
            }
        }
        Assert.InRange(readableCount, 1, 128);
        Assert.Equal("closed", service.Get(owner.PlayerId, closedSessionIds[^1]).State);
    }

    [Fact]
    public void ReclaimingClosedSessionsNeverEvictsAnActiveSession()
    {
        PlayerAccount activeOwner = Register("Active");
        PlayerAccount repeatOwner = Register("Repeat");
        SessionView active = Start(activeOwner);

        for (int index = 0; index < 130; index++)
        {
            SessionView temporary = Start(repeatOwner);
            service.Leave(repeatOwner.PlayerId, temporary.SessionId, temporary.Revision);
        }

        SessionView retained = service.Get(activeOwner.PlayerId, active.SessionId);
        Assert.Equal("lobby", retained.State);
        Assert.Equal(active.Revision, retained.Revision);
        Assert.Equal(active.Members, retained.Members);
        Assert.Equal(active.SessionId, Start(activeOwner).SessionId);
    }

    [Fact]
    public void FullActiveCapacityRejectsNewSessionsUntilAnOwnerClosesOne()
    {
        List<(PlayerAccount Owner, SessionView Session)> active = [];
        for (int index = 0; index < 128; index++)
        {
            PlayerAccount owner = Register($"Owner{index}");
            active.Add((owner, Start(owner)));
        }
        PlayerAccount waiting = Register("Waiting");

        AssertFault(503, "session_capacity_reached", () => Start(waiting));
        Assert.All(active, entry =>
        {
            SessionView retained = service.Get(entry.Owner.PlayerId, entry.Session.SessionId);
            Assert.Equal("lobby", retained.State);
            Assert.Equal(entry.Session.Revision, retained.Revision);
            Assert.Equal(entry.Session.Members, retained.Members);
        });
        Assert.Equal(active[0].Session.SessionId, Start(active[0].Owner).SessionId);

        (PlayerAccount leavingOwner, SessionView leaving) = active[0];
        service.Leave(leavingOwner.PlayerId, leaving.SessionId, leaving.Revision);
        SessionView admitted = Start(waiting);
        Assert.Equal(waiting.PlayerId, Assert.Single(admitted.Members).PlayerId);
        AssertFault(404, "session_not_found", () => service.Get(leavingOwner.PlayerId, leaving.SessionId));
        Assert.All(active.Skip(1), entry =>
            Assert.Equal("lobby", service.Get(entry.Owner.PlayerId, entry.Session.SessionId).State));
    }

    private PlayerAccount Register(string name, int classId = 1, int specializationId = 1)
    {
        PlayerCredential credential = store.ProvisionPlayer(clock.GetUtcNow());
        store.CreateCharacter(credential.PlayerId,
            new CreateCharacterRequest(name, classId, specializationId), Guid.NewGuid().ToString("D"));
        ProfileView profile = store.GetProfile(credential.PlayerId);
        Assert.NotNull(profile.Character);
        Assert.NotNull(profile.World);
        return new PlayerAccount(credential.PlayerId, profile.Character, profile.World);
    }

    private SessionView Start(PlayerAccount owner) => service.Start(owner.PlayerId,
        new StartSessionRequest(owner.Character.CharacterId, owner.World.PersonalWorldId,
            owner.World.Region.RegionInstanceId));

    private InviteView Invite(PlayerAccount owner, SessionView session) =>
        service.CreateInvite(owner.PlayerId, session.SessionId, session.Revision);

    private SessionView Join(PlayerAccount guest, InviteView invite) => service.Join(guest.PlayerId,
        new JoinSessionRequest(guest.Character.CharacterId, invite.InviteToken));

    private static void AssertFault(int status, string code, Action action)
    {
        ApiFault fault = Assert.Throws<ApiFault>(action);
        Assert.Equal(status, fault.StatusCode);
        Assert.Equal(code, fault.Code);
    }

    private static void AssertWorldEqual(WorldView expected, WorldView actual)
    {
        Assert.Equal(expected.PersonalWorldId, actual.PersonalWorldId);
        Assert.Equal(expected.OwnerPlayerId, actual.OwnerPlayerId);
        Assert.Equal(expected.Revision, actual.Revision);
        AssertRegionEqual(expected.Region, actual.Region);
    }

    private static void AssertRegionEqual(RegionPlan expected, RegionPlan actual)
    {
        Assert.Equal(expected.RegionInstanceId, actual.RegionInstanceId);
        Assert.Equal(expected.TemplateId, actual.TemplateId);
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.GenerationVersion, actual.GenerationVersion);
        Assert.Equal(expected.ContentVersion, actual.ContentVersion);
        Assert.Equal(expected.Nodes, actual.Nodes);
        Assert.Equal(expected.Edges, actual.Edges);
    }

    private static void CorruptArrays(SessionView view)
    {
        Assert.NotEmpty(view.Members);
        Assert.NotEmpty(view.Region.Nodes);
        Assert.NotEmpty(view.Region.Edges);
        view.Members[0] = new SessionMember(Guid.NewGuid(), Guid.NewGuid());
        view.Region.Nodes[0] = new RegionNode("tampered", "tampered", "tampered");
        view.Region.Edges[0] = new RegionEdge("tampered", "tampered");
    }

    public void Dispose()
    {
        DisposeStore();
        if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); }
    }

    private void DisposeStore()
    {
        if ((object)store is IDisposable disposable) { disposable.Dispose(); }
    }

    private sealed record PlayerAccount(Guid PlayerId, CharacterView Character, WorldView World);
    private sealed record AdmissionResult(SessionView? Session, ApiFault? Fault);

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void AdvanceTo(DateTimeOffset instant)
        {
            if (instant < utcNow) { throw new ArgumentOutOfRangeException(nameof(instant)); }
            utcNow = instant;
        }
    }
}
