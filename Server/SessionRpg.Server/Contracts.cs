namespace SessionRpg.Server
{
    // Persistent instance IDs are GUIDs, deliberately separate from Core's
    // authored WorldId/ZoneId and per-combat ParticipantId identities.
    public sealed record PlayerCredential(
        Guid PlayerId, string AccessToken, DateTimeOffset ExpiresAt);

    public sealed record CreateCharacterRequest(
        string Name, int ClassId, int SpecializationId);

    public sealed record CharacterView(
        Guid CharacterId, Guid PlayerId, string Name, int ClassId,
        int SpecializationId, int Level, long Experience, long Revision);

    // This is a saved regional progression graph, NOT walkable terrain or a
    // Unity asset manifest. Its templates are server-prototype identities only.
    public sealed record RegionNode(
        string NodeId, string Kind, string EncounterTemplateId);

    public sealed record RegionEdge(string FromNodeId, string ToNodeId);

    public sealed record RegionPlan(
        Guid RegionInstanceId, string TemplateId, int Seed,
        int GenerationVersion, int ContentVersion,
        RegionNode[] Nodes, RegionEdge[] Edges);

    public sealed record WorldView(
        Guid PersonalWorldId, Guid OwnerPlayerId, RegionPlan Region, long Revision);

    public sealed record ProfileView(
        Guid PlayerId, CharacterView? Character, WorldView? World);

    public sealed record StartSessionRequest(
        Guid CharacterId, Guid PersonalWorldId, Guid RegionInstanceId);

    public sealed record ExpectedRevisionRequest(long ExpectedRevision);

    public sealed record JoinSessionRequest(Guid CharacterId, string InviteToken);

    public sealed record SessionMember(Guid PlayerId, Guid CharacterId);

    public sealed record SessionView(
        Guid SessionId, Guid OwnerPlayerId, Guid PersonalWorldId,
        Guid RegionInstanceId, string State, long Revision,
        DateTimeOffset ExpiresAt, SessionMember[] Members, RegionPlan Region);

    public sealed record InviteView(
        Guid SessionId, string InviteToken, DateTimeOffset ExpiresAt, long Revision);

    public sealed record ErrorView(string Code);

    public sealed class ApiFault : Exception
    {
        public ApiFault(int statusCode, string code) : base(code)
        {
            StatusCode = statusCode;
            Code = code;
        }

        public int StatusCode { get; }
        public string Code { get; }
    }
}
