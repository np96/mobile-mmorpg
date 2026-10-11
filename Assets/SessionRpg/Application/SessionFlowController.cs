using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum GameModeId
    {
        Arena = 0,
        Boss = 1,
        Leveling = 2,
        HeroSiege = 3,
        Dungeon = 4,
        PveLeveling = 5,
    }

    public enum SessionPhase
    {
        Home,
        Countdown,
        Playing,
        Spectating,
        Results,
    }

    public readonly struct LegacyGameModeMigration
    {
        internal LegacyGameModeMigration(
            GameModeId legacyMode,
            PlayableModeId playableMode,
            RunKind runKind,
            EncounterKind encounterKind)
        {
            LegacyMode = legacyMode;
            PlayableMode = playableMode;
            RunKind = runKind;
            EncounterKind = encounterKind;
        }

        public GameModeId LegacyMode { get; }

        public PlayableModeId PlayableMode { get; }

        public RunKind RunKind { get; }

        public EncounterKind EncounterKind { get; }

        public bool IsTraining =>
            RunKind == SessionRpg.Core.RunKind.Training;
    }

    public static class LegacyGameModeMigrationAdapter
    {
        public static LegacyGameModeMigration Migrate(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.Arena:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.Arena,
                        RunKind.Match,
                        EncounterKind.Combat);
                case GameModeId.Boss:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.Dungeon,
                        RunKind.Sequential,
                        EncounterKind.Boss);
                case GameModeId.Leveling:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.None,
                        RunKind.Training,
                        EncounterKind.Combat);
                case GameModeId.HeroSiege:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.HeroSiege,
                        RunKind.WaveDefense,
                        EncounterKind.None);
                case GameModeId.Dungeon:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.Dungeon,
                        RunKind.Sequential,
                        EncounterKind.Combat);
                case GameModeId.PveLeveling:
                    return new LegacyGameModeMigration(
                        mode,
                        PlayableModeId.PveLeveling,
                        RunKind.PersistentRegion,
                        EncounterKind.Combat);
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unknown serialized game-mode identity.");
            }
        }
    }

    public readonly struct SessionViewState
    {
        public SessionViewState(
            SessionPhase phase,
            GameModeId mode,
            ClassId selectedClass,
            SpecializationId selectedSpecialization,
            int countdownTicks,
            int elapsedTicks,
            bool didWin,
            string resultReason)
        {
            Phase = phase;
            Mode = mode;
            SelectedClass = selectedClass;
            SelectedSpecialization = selectedSpecialization;
            CountdownTicks = countdownTicks;
            ElapsedTicks = elapsedTicks;
            DidWin = didWin;
            ResultReason = resultReason;
        }

        public SessionPhase Phase { get; }

        public GameModeId Mode { get; }

        public ClassId SelectedClass { get; }

        public SpecializationId SelectedSpecialization { get; }

        public int CountdownTicks { get; }

        public int ElapsedTicks { get; }

        public bool DidWin { get; }

        public string ResultReason { get; }
    }

    public sealed class SessionFlowController
    {
        private int _countdownTicks;
        private int _elapsedTicks;
        private bool _didWin;
        private string _resultReason;

        public SessionFlowController()
        {
            Phase = SessionPhase.Home;
            Mode = GameModeId.Arena;
            SelectedClass = ClassId.Mage;
            SelectedSpecialization = SpecializationId.Frost;
            _resultReason = string.Empty;
        }

        public SessionPhase Phase { get; private set; }

        public GameModeId Mode { get; private set; }

        public ClassId SelectedClass { get; private set; }

        public SpecializationId SelectedSpecialization { get; private set; }

        public SessionViewState ViewState => new SessionViewState(
            Phase,
            Mode,
            SelectedClass,
            SelectedSpecialization,
            _countdownTicks,
            _elapsedTicks,
            _didWin,
            _resultReason);

        public void SelectClass(ClassId classId)
        {
            if (Phase != SessionPhase.Home ||
                (classId != ClassId.Healer &&
                 classId != ClassId.Rogue &&
                 classId != ClassId.Mage &&
                 classId != ClassId.Hunter))
            {
                return;
            }

            SelectedClass = classId;
            if (!SpecializationCatalog.IsValidForClass(classId, SelectedSpecialization))
            {
                SelectedSpecialization = SpecializationCatalog.GetDefault(classId);
            }
        }

        public void SelectSpecialization(SpecializationId specialization)
        {
            if (Phase != SessionPhase.Home ||
                !SpecializationCatalog.IsValidForClass(SelectedClass, specialization))
            {
                return;
            }

            SelectedSpecialization = specialization;
        }

        public void Start(GameModeId mode, int countdownTicks)
        {
            if (Phase != SessionPhase.Home && Phase != SessionPhase.Results)
            {
                return;
            }

            if (mode != GameModeId.Arena &&
                mode != GameModeId.Boss &&
                mode != GameModeId.Leveling &&
                mode != GameModeId.HeroSiege &&
                mode != GameModeId.Dungeon &&
                mode != GameModeId.PveLeveling)
            {
                return;
            }

            Mode = mode;
            Phase = countdownTicks > 0 ? SessionPhase.Countdown : SessionPhase.Playing;
            _countdownTicks = countdownTicks > 0 ? countdownTicks : 0;
            _elapsedTicks = 0;
            _didWin = false;
            _resultReason = string.Empty;
        }

        public void Tick()
        {
            if (Phase == SessionPhase.Countdown)
            {
                _countdownTicks--;
                if (_countdownTicks <= 0)
                {
                    _countdownTicks = 0;
                    Phase = SessionPhase.Playing;
                }
            }
            else if (Phase == SessionPhase.Playing || Phase == SessionPhase.Spectating)
            {
                _elapsedTicks++;
            }
        }

        public void EnterSpectating()
        {
            if (Phase == SessionPhase.Playing)
            {
                Phase = SessionPhase.Spectating;
            }
        }

        public void ResumePlaying()
        {
            if (Phase == SessionPhase.Spectating)
            {
                Phase = SessionPhase.Playing;
            }
        }

        public void Complete(bool didWin, string reason)
        {
            if (Phase != SessionPhase.Playing && Phase != SessionPhase.Spectating)
            {
                return;
            }

            _didWin = didWin;
            _resultReason = reason ?? string.Empty;
            Phase = SessionPhase.Results;
        }

        public void ReturnHome()
        {
            Phase = SessionPhase.Home;
            _countdownTicks = 0;
            _elapsedTicks = 0;
            _didWin = false;
            _resultReason = string.Empty;
        }
    }
}
