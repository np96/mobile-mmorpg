#if UNITY_EDITOR || DEVELOPMENT_BUILD || SESSION_RPG_CAPTURE
using System;
using System.Collections;
using System.IO;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using SessionRpg.Unity.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SessionRpg.Unity.Gameplay
{
    /// <summary>Android's screenshot API prepends its persistent directory.</summary>
    public static class SessionRpgCapturePath
    {
        public static string ScreenshotFileName(string outputPath, string persistentDirectory, bool android)
        {
            if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathRooted(outputPath))
            {
                throw new ArgumentException("Reference capture requires an absolute output path.");
            }
            string absoluteOutput = Path.GetFullPath(outputPath);
            if (!android) { return absoluteOutput; }
            if (string.IsNullOrWhiteSpace(persistentDirectory) || !Path.IsPathRooted(persistentDirectory))
            {
                throw new ArgumentException("Android reference capture requires an absolute persistent directory.");
            }
            string prefix = Path.GetFullPath(persistentDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absoluteOutput.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new ArgumentException("Android reference capture output must be inside Application.persistentDataPath: " + prefix);
            }
            return absoluteOutput.Substring(prefix.Length);
        }
    }

    /// <summary>
    /// Development-player hook for capturing one deterministic-looking arena reference frame.
    /// It is inert unless the player is launched with
    /// <c>-sessionRpgCapture /absolute/path/to/frame.png</c>.
    /// </summary>
    internal sealed class SessionRpgReferenceCapture : MonoBehaviour
    {
        private const string CaptureArgument = "-sessionRpgCapture";
        private const string CaptureScreenArgument = "-sessionRpgCaptureScreen";
        private const string CaptureModeArgument = "-sessionRpgCaptureMode";
        private const string CaptureSiegeVariantArgument =
            "-sessionRpgCaptureSiegeVariant";
        private const string CaptureSpecializationArgument = "-sessionRpgCaptureSpecialization";
        private const string CaptureAbilityArgument = "-sessionRpgCaptureAbility";
        private const string CaptureSwipeAbilityArgument =
            "-sessionRpgCaptureSwipeAbility";
        private const string CaptureEffectDelayArgument = "-sessionRpgCaptureEffectDelay";
        private const string CaptureCameraYawArgument = "-sessionRpgCaptureCameraYaw";
        private const string CaptureCameraPitchArgument = "-sessionRpgCaptureCameraPitch";
        private const string CaptureCameraDistanceArgument = "-sessionRpgCaptureCameraDistance";
        private const string CaptureCameraFocusArgument = "-sessionRpgCaptureCameraFocus";
        private const string CaptureCameraRightFocusArgument = "-sessionRpgCaptureCameraRightFocus";
        private const string CaptureCameraFieldOfViewArgument = "-sessionRpgCaptureCameraFov";
        private const float CountdownAndSettleSeconds = 4.25f;
        private const float SiegeCountdownAndSettleSeconds = 22.25f;
        private const float DefaultEffectDelaySeconds = 0.14f;
        private const float WriteTimeoutSeconds = 10f;

        private static bool _started;

        private string _outputPath;
        private string _screenshotFileName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (_started || !TryGetOutputPath(out string outputPath))
            {
                return;
            }

            _started = true;
            string screenshotFileName;
            try
            {
                screenshotFileName = SessionRpgCapturePath.ScreenshotFileName(outputPath,
                    UnityEngine.Application.persistentDataPath,
                    UnityEngine.Application.platform == RuntimePlatform.Android);
            }
            catch (ArgumentException error)
            {
                Debug.LogError(error.Message);
                Quit();
                return;
            }
            UnityEngine.Application.runInBackground = true;
            GameObject runner = new GameObject(nameof(SessionRpgReferenceCapture));
            DontDestroyOnLoad(runner);
            SessionRpgReferenceCapture capture = runner.AddComponent<SessionRpgReferenceCapture>();
            capture._outputPath = outputPath;
            capture._screenshotFileName = screenshotFileName;
            capture.StartCoroutine(capture.CaptureReferenceFrame());
        }

        private static bool TryGetOutputPath(out string outputPath)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], CaptureArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                string candidate = arguments[index + 1];
                if (!Path.IsPathRooted(candidate) ||
                    !string.Equals(Path.GetExtension(candidate), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                outputPath = Path.GetFullPath(candidate);
                return true;
            }

            outputPath = string.Empty;
            return false;
        }

        private IEnumerator CaptureReferenceFrame()
        {
            PrototypeGameController controller = null;
            float discoveryDeadline = Time.realtimeSinceStartup + WriteTimeoutSeconds;
            while (controller == null && Time.realtimeSinceStartup < discoveryDeadline)
            {
                controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                yield return null;
            }

            if (controller == null)
            {
                Debug.LogError("Session RPG reference capture could not find PrototypeGameController.");
                Quit();
                yield break;
            }

            bool captureHome = string.Equals(
                ReadStringArgument(CaptureScreenArgument, string.Empty),
                "Home",
                StringComparison.OrdinalIgnoreCase);
            if (captureHome)
            {
                yield return null;
                PrototypeHud hud = UnityEngine.Object
                    .FindAnyObjectByType<PrototypeHud>();
                hud?.SetTrainingRecommendation(true);
                hud?.SetSelectedMode(PrototypeMode.Training);
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return new WaitForSecondsRealtime(0.75f);
            }
            else
            {
                GameModeId mode = ReadEnumArgument(
                    CaptureModeArgument,
                    GameModeId.Arena);
                SpecializationId specialization = ReadEnumArgument(
                    CaptureSpecializationArgument,
                    SpecializationId.Frost);
                ClassId classId = SpecializationCatalog.GetDefinition(specialization).OwnerClass;
                if (mode == GameModeId.PveLeveling)
                {
                    controller.ConfigureGrindingCapture(
                        Path.Combine(Path.GetDirectoryName(_outputPath), "pve-capture-save"),
                        (int)ReadFloatArgument("-sessionRpgCaptureCamp", -1f),
                        ReadFloatArgument("-sessionRpgCaptureCampLateralOffset", 0f));
                }
                if (mode == GameModeId.HeroSiege)
                {
                    SiegeVariantId variant = ReadEnumArgument(
                        CaptureSiegeVariantArgument,
                        SiegeVariantId.SoloLane);
                    controller.StartLocalSiegeSession(
                        variant,
                        classId,
                        specialization);
                }
                else
                {
                    controller.StartLocalSession(mode, classId, specialization);
                }
                if (mode == GameModeId.Leveling || mode == GameModeId.PveLeveling)
                {
                    WorldZoneStreamController streamer =
                        UnityEngine.Object.FindAnyObjectByType<WorldZoneStreamController>();
                    float streamDeadline =
                        Time.realtimeSinceStartup + WriteTimeoutSeconds;
                    while (streamer != null &&
                           !streamer.IsInitialWindowReady &&
                           Time.realtimeSinceStartup < streamDeadline)
                    {
                        yield return null;
                    }

                    if (streamer == null || !streamer.IsInitialWindowReady)
                    {
                        Debug.LogError(
                            "Session RPG reference capture timed out waiting for " +
                            "the initial world chunk window.");
                        Quit();
                        yield break;
                    }
                }

                ApplyCaptureCameraComposition();
                if (mode == GameModeId.PveLeveling)
                {
                    // Cold rendering/OS throttling can consume wall time without
                    // advancing the fixed simulation. Never accept a countdown
                    // screenshot as evidence of an active grinding camp.
                    float settleDeadline = Time.realtimeSinceStartup + 30f;
                    while (!controller.IsGrindingCaptureReady && Time.realtimeSinceStartup < settleDeadline)
                    {
                        yield return null;
                    }
                    if (!controller.IsGrindingCaptureReady)
                    {
                        Debug.LogError("PvE reference capture timed out before the simulation settled.");
                        Quit();
                        yield break;
                    }
                }
                else
                {
                    yield return new WaitForSecondsRealtime(
                        mode == GameModeId.HeroSiege
                            ? SiegeCountdownAndSettleSeconds
                            : CountdownAndSettleSeconds);
                }

                AbilityId showcaseAbility = ReadEnumArgument(
                    CaptureAbilityArgument,
                    AbilityId.None);
                if (showcaseAbility != AbilityId.None)
                {
                    PresentAbilityShowcase(
                        controller,
                        specialization,
                        showcaseAbility);
                    float effectDelay = ReadFloatArgument(
                        CaptureEffectDelayArgument,
                        DefaultEffectDelaySeconds);
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, effectDelay));
                }
                else
                {
                    PresentRestingTableau(controller);
                    yield return new WaitForSecondsRealtime(0.15f);
                }

                AbilityId swipeAbility = ReadEnumArgument(
                    CaptureSwipeAbilityArgument,
                    AbilityId.None);
                if (swipeAbility != AbilityId.None)
                {
                    PresentSwipePalette(swipeAbility);
                    yield return null;
                }
            }

            string directory = Path.GetDirectoryName(_outputPath);
            if (string.IsNullOrEmpty(directory))
            {
                Debug.LogError("Session RPG reference capture has no output directory.");
                Quit();
                yield break;
            }

            try
            {
                Directory.CreateDirectory(directory);
                if (File.Exists(_outputPath))
                {
                    File.Delete(_outputPath);
                }

                // Android prepends persistentDataPath even to an absolute
                // filename. Poll the absolute destination, but submit the
                // verified relative filename on that platform only.
                ScreenCapture.CaptureScreenshot(_screenshotFileName, 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Quit();
                yield break;
            }

            float writeDeadline = Time.realtimeSinceStartup + WriteTimeoutSeconds;
            long previousLength = -1L;
            int stableFrameCount = 0;
            while (stableFrameCount < 2 && Time.realtimeSinceStartup < writeDeadline)
            {
                if (File.Exists(_outputPath))
                {
                    long length = new FileInfo(_outputPath).Length;
                    stableFrameCount = length > 0L && length == previousLength
                        ? stableFrameCount + 1
                        : 0;
                    previousLength = length;
                }

                yield return null;
            }

            if (stableFrameCount >= 2)
            {
                Debug.Log("Session RPG reference frame captured: " + _outputPath);
            }
            else
            {
                Debug.LogError("Session RPG reference capture timed out: " + _outputPath);
            }

            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private static T ReadEnumArgument<T>(string argumentName, T fallback)
            where T : struct
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal) &&
                    Enum.TryParse(arguments[index + 1], true, out T value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static float ReadFloatArgument(string argumentName, float fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal) &&
                    float.TryParse(
                        arguments[index + 1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static string ReadStringArgument(
            string argumentName,
            string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        argumentName,
                        StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return fallback;
        }

        private static void ApplyCaptureCameraComposition()
        {
            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;
            if (gameCamera == null)
            {
                return;
            }

            SessionRpg.Unity.Camera.StrategyFollowCamera cameraRig =
                gameCamera.GetComponent<SessionRpg.Unity.Camera.StrategyFollowCamera>();
            if (cameraRig == null)
            {
                return;
            }

            float yaw = ReadFloatArgument(CaptureCameraYawArgument, cameraRig.Yaw);
            float pitch = ReadFloatArgument(CaptureCameraPitchArgument, cameraRig.Pitch);
            float distance = ReadFloatArgument(
                CaptureCameraDistanceArgument,
                cameraRig.Distance);
            float focus = ReadFloatArgument(
                CaptureCameraFocusArgument,
                cameraRig.ForwardFocusOffset);
            float rightFocus = ReadFloatArgument(
                CaptureCameraRightFocusArgument,
                cameraRig.RightFocusOffset);
            float fieldOfView = ReadFloatArgument(
                CaptureCameraFieldOfViewArgument,
                cameraRig.FieldOfView);
            bool projectionOverride = ReadStringArgument(CaptureCameraPitchArgument, null) != null ||
                ReadStringArgument(CaptureCameraDistanceArgument, null) != null ||
                ReadStringArgument(CaptureCameraFocusArgument, null) != null ||
                ReadStringArgument(CaptureCameraRightFocusArgument, null) != null ||
                ReadStringArgument(CaptureCameraFieldOfViewArgument, null) != null;
            ApplyCaptureCameraValues(cameraRig,
                yaw,
                pitch,
                distance,
                focus,
                rightFocus,
                fieldOfView,
                projectionOverride);
            Debug.Log(
                "Session RPG capture camera: yaw=" + cameraRig.Yaw.ToString("0.##") +
                ", pitch=" + cameraRig.Pitch.ToString("0.##") +
                ", distance=" + cameraRig.Distance.ToString("0.##") +
                ", focus=" + cameraRig.ForwardFocusOffset.ToString("0.##") +
                ", rightFocus=" + cameraRig.RightFocusOffset.ToString("0.##") +
                ", fov=" + cameraRig.FieldOfView.ToString("0.##") + ".");
        }

        private static void ApplyCaptureCameraValues(
            SessionRpg.Unity.Camera.StrategyFollowCamera cameraRig,
            float yaw, float pitch, float distance, float focus, float rightFocus,
            float fieldOfView, bool projectionOverride)
        {
            if (!projectionOverride)
            {
                // Ordinary captures and yaw-only orbit retain the actual mode
                // projection, including PvE's lower angle, sky and far plane.
                cameraRig.SetHeading(yaw, true);
                return;
            }
            // Explicit lens/pitch look-development keeps the legacy public
            // setter's safety clamps; the log above reports the resolved values.
            cameraRig.SetFixedComposition(yaw, pitch, distance, focus, rightFocus,
                fieldOfView, true);
        }

        private static void PresentAbilityShowcase(
            PrototypeGameController controller,
            SpecializationId specialization,
            AbilityId ability)
        {
            if (!AbilityCatalog.TryGet(ability, out AbilityDefinition definition))
            {
                Debug.LogError("Session RPG showcase does not recognize ability " + ability + ".");
                return;
            }

            CombatantView[] combatants = UnityEngine.Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            CombatantView actor = null;
            CombatantView hostileTarget = null;
            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView candidate = combatants[index];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                if (candidate.IsHuman)
                {
                    actor = candidate;
                    continue;
                }

                if (candidate.Team.Value != 1 && !candidate.IsHiddenFromViewer)
                {
                    if (hostileTarget == null ||
                        candidate.Id.Value < hostileTarget.Id.Value)
                    {
                        hostileTarget = candidate;
                    }
                }
            }

            if (actor == null)
            {
                Debug.LogError("Session RPG showcase could not find the human combatant.");
                return;
            }

            bool targetSelf = definition.Targeting.Shape == AbilityTargetShape.CasterArea ||
                !definition.Targeting.Allows(AbilityTargetRelation.Hostile);
            CombatantView target = targetSelf || hostileTarget == null
                ? actor
                : hostileTarget;
            if (target != actor)
            {
                PrototypeHud hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
                if (hud != null)
                {
                    hud.SelectFighter(target.Id);
                }
            }

            // The live controller has already evaluated readiness against the
            // pre-capture bot positions. Rebinding the same kit is a review-only
            // reset that removes those stale LOS/RANGE badges without changing
            // readiness evaluation during normal play.
            ResetHudForStaticReview(specialization);

            // Freeze a small, reproducible combat tableau. These captures are a visual
            // acceptance tool, so the showcased gesture and effect must not be obscured
            // by a bot wandering away between launch and screenshot frames.
            controller.enabled = false;

            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView candidate = combatants[index];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate != actor && candidate != target)
                {
                    candidate.gameObject.SetActive(false);
                    continue;
                }

                ResetCombatantForStaticReview(candidate);
            }

            if (target != actor)
            {
                Vector3 center = new Vector3(-0.15f, 0f, 0.075f);
                UnityEngine.Camera stagingCamera = UnityEngine.Camera.main;
                Vector3 cameraRight = stagingCamera != null
                    ? stagingCamera.transform.right
                    : Vector3.right;
                cameraRight.y = 0f;
                cameraRight = cameraRight.sqrMagnitude > 0.001f
                    ? cameraRight.normalized
                    : Vector3.right;
                Vector3 actorPosition = center - cameraRight * 1.55f;
                Vector3 targetPosition = center + cameraRight * 1.55f;
                Vector3 facing = targetPosition - actorPosition;
                facing.y = 0f;
                actor.transform.SetPositionAndRotation(
                    actorPosition,
                    Quaternion.LookRotation(facing.normalized, Vector3.up));
                target.transform.SetPositionAndRotation(
                    targetPosition,
                    Quaternion.LookRotation(-facing.normalized, Vector3.up));
            }
            else
            {
                actor.transform.position = new Vector3(0f, 0f, 0.15f);
            }

            HunterCompanionView companion = actor.CompanionVisuals;
            if (companion != null && companion.IsVisible)
            {
                companion.transform.position = actor.transform.position +
                    new Vector3(-0.92f, 0.04f, -0.72f);
                companion.transform.rotation = actor.transform.rotation;
                companion.enabled = false;
            }

            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;
            if (gameCamera != null)
            {
                SessionRpg.Unity.Camera.StrategyFollowCamera cameraRig =
                    gameCamera.GetComponent<SessionRpg.Unity.Camera.StrategyFollowCamera>();
                cameraRig?.SetFollowTarget(actor.transform, true);
            }

            AbilityActivationType activationType =
                definition.CastProfile.ActivationType;
            if (activationType == AbilityActivationType.Instant)
            {
                actor.PlayCastAnimation(ability);
            }
            else if (activationType == AbilityActivationType.Channel)
            {
                actor.PlayCastAnimation(ability);
                actor.PlayChannelPulseAnimation(ability);
            }
            else
            {
                actor.PlayCastReleaseAnimation(ability);
            }

            CombatEffectsPresenter effects =
                UnityEngine.Object.FindAnyObjectByType<CombatEffectsPresenter>();
            if (effects == null)
            {
                Debug.LogError("Session RPG showcase could not find CombatEffectsPresenter.");
                return;
            }

            if (activationType == AbilityActivationType.Channel)
            {
                effects.PresentChannelTick(
                    ability,
                    actor.EffectOrigin,
                    target.AimPoint,
                    target.AimPoint.position);
            }
            else
            {
                Vector3 resolvedGroundPoint =
                    definition.Targeting.Shape == AbilityTargetShape.CasterArea
                        ? actor.transform.position
                        : target.transform.position;
                effects.PresentCastCompleted(
                    ability,
                    actor.EffectOrigin,
                    target.AimPoint,
                    resolvedGroundPoint);
            }
            if (AbilityPresentationCatalog.TryGet(
                    ability,
                    out AbilityPresentation showcasePresentation) &&
                showcasePresentation.Motion == EffectMotion.Projectile)
            {
                // The development reference frame deliberately shows the
                // mid-flight lane and its grounded destination response together.
                effects.PresentImpact(ability, null, target.transform.position);
            }
            Debug.Log("Session RPG showcase staged " + ability + ".");
        }

        private static void ResetHudForStaticReview(
            SpecializationId specialization)
        {
            PrototypeHud hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
            if (hud == null)
            {
                return;
            }

            hud.BindSpecializationKit(specialization);
            hud.SetAbilityControlsInteractable(true);
            hud.SetGlobalCooldown(0f, 0f, false);
        }

        private static void PresentSwipePalette(AbilityId primaryAbility)
        {
            AbilitySlotView[] slots = UnityEngine.Object.FindObjectsByType<AbilitySlotView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < slots.Length; index++)
            {
                AbilitySlotView slot = slots[index];
                if (slot == null || slot.AbilityId != primaryAbility)
                {
                    continue;
                }

                AbilityButton button = slot.GetComponent<AbilityButton>();
                if (button == null)
                {
                    break;
                }

                var pointer = new PointerEventData(EventSystem.current)
                {
                    pointerId = 177,
                    button = PointerEventData.InputButton.Left,
                    position = Vector2.zero,
                };
                button.OnPointerDown(pointer);
                Debug.Log("Session RPG swipe palette staged for " + primaryAbility + ".");
                return;
            }

            Debug.LogError(
                "Session RPG swipe palette could not find primary ability " +
                primaryAbility + ".");
        }

        private static void PresentRestingTableau(PrototypeGameController controller)
        {
            // A normal review plate must show the authored idle silhouette. Freezing
            // an arbitrary live frame can otherwise preserve a one-frame hit or
            // knockback pose and make a grounded fighter appear to be falling.
            controller.enabled = false;
            CombatantView[] combatants = UnityEngine.Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView combatant = combatants[index];
                if (combatant == null || !combatant.IsAlive)
                {
                    continue;
                }

                ResetCombatantForStaticReview(combatant);
            }
        }

        private static void ResetCombatantForStaticReview(CombatantView combatant)
        {
            Vector3 groundedPosition = combatant.transform.position;
            groundedPosition.y = 0f;
            combatant.transform.position = groundedPosition;
            combatant.ForceVisibleForStaticReview();
            combatant.enabled = false;
            combatant.AuraVisuals?.ResetPresentation();

            CombatantAnimationPresenter animationPresenter =
                combatant.GetComponent<CombatantAnimationPresenter>();
            if (animationPresenter == null)
            {
                return;
            }

            animationPresenter.Initialize(combatant.Class);
            animationPresenter.ConfigureRelation(
                combatant.IsHuman,
                combatant.Team.Value == 1,
                combatant.Class == ClassId.Boss);
            animationPresenter.Present(0f, Vector2.zero, true);
            animationPresenter.ClearCastState();
            animationPresenter.SetControlVisualState(
                CombatantControlVisualState.None);
            animationPresenter.SetDefenseVisualState(0f, 0f);
        }
    }
}
#endif
