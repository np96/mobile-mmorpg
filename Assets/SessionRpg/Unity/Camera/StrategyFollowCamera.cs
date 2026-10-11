using System;
using SessionRpg.Application;
using UnityEngine;

namespace SessionRpg.Unity.Camera
{
    /// <summary>
    /// An elevated perspective camera for touch-first strategy combat. It follows
    /// one combatant and accepts yaw-only orbit requests; pitch, distance, and
    /// zoom stay authored so encounter readability remains bounded.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class StrategyFollowCamera : MonoBehaviour
    {
        public const float ArenaYaw = 90f;
        public const float ArenaPitch = 40f;
        public const float ArenaDistance = 18f;
        public const float ArenaForwardFocusOffset = 2.3f;
        public const float ArenaRightFocusOffset = 0f;
        public const float ArenaFieldOfView = 46f;
        public const float ArenaMaximumOrbitOffset = 60f;

        public const float BossYaw = 0f;
        public const float BossPitch = 56f;
        public const float BossDistance = 22f;
        public const float BossForwardFocusOffset = 2.3f;
        public const float BossRightFocusOffset = 0f;
        public const float BossFieldOfView = 50f;
        public const float BossOrbitCenterYaw = 15f;
        public const float BossMaximumOrbitOffset = 50f;

        public const float SiegeSoloYaw = 90f;
        public const float SiegeSoloPitch = 58f;
        public const float SiegeSoloDistance = 19f;
        public const float SiegeSoloForwardFocusOffset = 2.4f;
        public const float SiegeSoloFieldOfView = 46f;
        public const float SiegeFourLaneYaw = 45f;
        public const float SiegeFourLanePitch = 65f;
        public const float SiegeFourLaneDistance = 24f;
        public const float SiegeFourLaneRightFocusOffset = 3f;
        public const float SiegeFourLaneFieldOfView = 50f;

        public const float LevelingYaw = 90f;
        public const float LevelingPitch = 56f;
        public const float LevelingDistance = 17f;
        public const float LevelingForwardFocusOffset = 4.1f;
        public const float LevelingRightFocusOffset = 1.15f;
        public const float LevelingFieldOfView = 45f;
        public const float LevelingMaximumOrbitOffset = 55f;

        // Persistent mob grinding has its own oblique landscape composition.
        // Keep the guided Training route's reviewed framing unchanged.
        public const float PveLevelingYaw = 70f;
        public const float PveLevelingPitch = 20f;
        public const float PveLevelingDistance = 18f;
        public const float PveLevelingForwardFocusOffset = 6f;
        public const float PveLevelingRightFocusOffset = 2.5f;
        public const float PveLevelingFieldOfView = 58f;
        public const float PveLevelingFarClipPlane = 400f;
        private const float StrategyMinimumPitch = 35f;
        // An open region has no privileged eastward approach. Allow a complete
        // orbit while retaining the fixed pitch, distance, and player follow.
        public const float PveLevelingMaximumOrbitOffset = 180f;

        [SerializeField] private Transform _followTarget;

        [Header("Fixed strategy view")]
        [SerializeField, Range(0f, 360f)] private float _yaw = ArenaYaw;
        [SerializeField, Range(0f, 360f)] private float _orbitCenterYaw = ArenaYaw;
        [SerializeField, Range(0f, 180f)] private float _maximumOrbitOffset =
            ArenaMaximumOrbitOffset;
        [SerializeField, Range(20f, 80f)] private float _pitch = ArenaPitch;
        [SerializeField, HideInInspector] private bool _pveLandscapeComposition;
        [SerializeField, Min(1f)] private float _followDistance = ArenaDistance;
        [SerializeField, Min(0f)] private float _pivotHeight = 0.9f;
        [SerializeField, Min(0f)] private float _forwardFocusOffset =
            ArenaForwardFocusOffset;
        [SerializeField, Range(-10f, 10f)] private float _rightFocusOffset =
            ArenaRightFocusOffset;
        [SerializeField, Range(35f, 70f)] private float _fieldOfView =
            ArenaFieldOfView;
        [SerializeField, Min(0f)] private float _followSmoothTime = 0.1f;

        [Header("Combat response")]
        [SerializeField, Min(0f)] private float _maximumImpulseDistance = 0.16f;
        [SerializeField, Min(0.1f)] private float _impulseDecay = 7.5f;
        [SerializeField, Min(1f)] private float _impulseFrequency = 24f;

        private Vector3 _positionVelocity;
        private float _combatImpulse;
        private bool _reducedMotion;
        private bool _pvePresentationApplied;
        private float _baselineFarClipPlane;
        private CameraClearFlags _baselineClearFlags;

        public Transform FollowTarget => _followTarget;

        public float Yaw => _yaw;

        public float OrbitCenterYaw => _orbitCenterYaw;

        public float MaximumOrbitOffset => _maximumOrbitOffset;

        public float Pitch => _pitch;

        public float Distance => _followDistance;

        public float ForwardFocusOffset => _forwardFocusOffset;

        public float RightFocusOffset => _rightFocusOffset;

        public float FieldOfView => _fieldOfView;

        /// <summary>
        /// When enabled, combat camera impulses are suppressed. Fixed camera
        /// framing and ordinary follow smoothing are intentionally unaffected.
        /// </summary>
        public bool ReducedMotion => _reducedMotion;

        public Vector3 PlanarForward
        {
            get
            {
                Vector3 forward = FixedRotation * Vector3.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : Vector3.forward;
            }
        }

        private Quaternion FixedRotation => Quaternion.Euler(_pitch, _yaw, 0f);

        private void OnEnable()
        {
            if (_pveLandscapeComposition) { ApplyPvePresentationSettings(); }
            ApplyProjection();
            if (_followTarget != null)
            {
                SnapToTarget();
            }
            else
            {
                transform.rotation = FixedRotation;
            }
        }

        private void OnDisable()
        {
            RestorePvePresentationSettings();
        }

        private void OnDestroy()
        {
            RestorePvePresentationSettings();
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
            {
                UpdateCamera(false);
            }
        }

        public void SetFollowTarget(Transform target, bool snap = true)
        {
            _followTarget = target;
            if (snap && target != null)
            {
                SnapToTarget();
            }
        }

        public void SetHeading(float yaw, bool snap = true)
        {
            float offset = Mathf.DeltaAngle(_orbitCenterYaw, yaw);
            _yaw = Mathf.Repeat(
                _orbitCenterYaw + Mathf.Clamp(
                    offset,
                    -_maximumOrbitOffset,
                    _maximumOrbitOffset),
                360f);
            if (snap && _followTarget != null)
            {
                SnapToTarget();
            }
            else
            {
                transform.rotation = FixedRotation;
            }
        }

        public void RotateHeading(float yawDelta, bool snap = false)
        {
            if (Mathf.Abs(yawDelta) <= 0.0001f)
            {
                return;
            }

            SetHeading(_yaw + yawDelta, snap);
        }

        /// <summary>
        /// Restores the fixed composition for a gameplay mode. Arena framing keeps
        /// the complete safe floor and its fortress silhouette readable behind the
        /// touch HUD; the larger boss encounter retains its objective-wide view.
        /// </summary>
        public void SetGameplayComposition(GameModeId mode, bool snap = true)
        {
            switch (mode)
            {
                case GameModeId.Arena:
                    SetFixedComposition(
                        ArenaYaw,
                        ArenaPitch,
                        ArenaDistance,
                        ArenaForwardFocusOffset,
                        ArenaRightFocusOffset,
                        ArenaFieldOfView,
                        snap);
                    SetOrbitBounds(
                        ArenaYaw,
                        ArenaMaximumOrbitOffset,
                        snap);
                    break;

                case GameModeId.Boss:
                case GameModeId.Dungeon:
                    SetFixedComposition(
                        BossYaw,
                        BossPitch,
                        BossDistance,
                        BossForwardFocusOffset,
                        BossRightFocusOffset,
                        BossFieldOfView,
                        snap);
                    SetOrbitBounds(
                        BossOrbitCenterYaw,
                        BossMaximumOrbitOffset,
                        snap);
                    break;

                case GameModeId.HeroSiege:
                    SetSiegeComposition(false, snap);
                    break;

                case GameModeId.PveLeveling:
                    SetAuthoredComposition(
                        PveLevelingYaw,
                        PveLevelingPitch,
                        PveLevelingDistance,
                        PveLevelingForwardFocusOffset,
                        PveLevelingRightFocusOffset,
                        PveLevelingFieldOfView,
                        snap,
                        true);
                    SetOrbitBounds(
                        PveLevelingYaw,
                        PveLevelingMaximumOrbitOffset,
                        snap);
                    break;

                case GameModeId.Leveling:
                    SetFixedComposition(
                        LevelingYaw,
                        LevelingPitch,
                        LevelingDistance,
                        LevelingForwardFocusOffset,
                        LevelingRightFocusOffset,
                        LevelingFieldOfView,
                        snap);
                    SetOrbitBounds(
                        LevelingYaw,
                        LevelingMaximumOrbitOffset,
                        snap);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public void SetSiegeComposition(bool fourLanes, bool snap = true)
        {
            SetFixedComposition(
                fourLanes ? SiegeFourLaneYaw : SiegeSoloYaw,
                fourLanes ? SiegeFourLanePitch : SiegeSoloPitch,
                fourLanes ? SiegeFourLaneDistance : SiegeSoloDistance,
                fourLanes ? 0f : SiegeSoloForwardFocusOffset,
                fourLanes ? SiegeFourLaneRightFocusOffset : 0f,
                fourLanes ? SiegeFourLaneFieldOfView : SiegeSoloFieldOfView,
                snap);
            SetOrbitBounds(
                fourLanes ? SiegeFourLaneYaw : SiegeSoloYaw,
                180f,
                snap);
        }

        /// <summary>
        /// Applies a complete authored composition. Runtime yaw orbit may adjust
        /// the heading afterward, while pitch, distance, offsets, and zoom remain
        /// fixed for the selected encounter.
        /// </summary>
        public void SetFixedComposition(
            float yaw,
            float pitch,
            float distance,
            float forwardFocusOffset,
            float rightFocusOffset,
            float fieldOfView,
            bool snap = true)
        {
            SetAuthoredComposition(yaw, pitch, distance, forwardFocusOffset,
                rightFocusOffset, fieldOfView, snap, false);
        }

        private void SetAuthoredComposition(
            float yaw,
            float pitch,
            float distance,
            float forwardFocusOffset,
            float rightFocusOffset,
            float fieldOfView,
            bool snap,
            bool pveLandscape)
        {
            // Only the explicit PvE mode admits the lower landscape angle. The
            // public look-development setter and every other mode retain their
            // existing 35-degree safety clamp.
            _pveLandscapeComposition = pveLandscape;
            if (pveLandscape)
            {
                if (isActiveAndEnabled) { ApplyPvePresentationSettings(); }
            }
            else { RestorePvePresentationSettings(); }
            _yaw = Mathf.Repeat(yaw, 360f);
            _orbitCenterYaw = _yaw;
            _maximumOrbitOffset = 180f;
            _pitch = Mathf.Clamp(pitch, pveLandscape ? PveLevelingPitch : StrategyMinimumPitch, 80f);
            _followDistance = Mathf.Max(1f, distance);
            _forwardFocusOffset = Mathf.Max(0f, forwardFocusOffset);
            _rightFocusOffset = Mathf.Clamp(rightFocusOffset, -10f, 10f);
            _fieldOfView = Mathf.Clamp(fieldOfView, 35f, 70f);
            ApplyProjection();

            if (snap && _followTarget != null)
            {
                SnapToTarget();
            }
            else
            {
                transform.rotation = FixedRotation;
            }
        }

        /// <summary>
        /// Limits user orbit to a camera-reviewed arc around the authored mode
        /// heading. This keeps presentation-only foreground architecture from
        /// crossing the combat space while preserving useful observation angles.
        /// Direct look-development composition remains available through
        /// <see cref="SetFixedComposition"/>, which resets the arc to 360 degrees.
        /// </summary>
        public void SetOrbitBounds(
            float centerYaw,
            float maximumOffset,
            bool snap = true)
        {
            _orbitCenterYaw = Mathf.Repeat(centerYaw, 360f);
            _maximumOrbitOffset = Mathf.Clamp(maximumOffset, 0f, 180f);
            SetHeading(_yaw, snap);
        }

        public void AddCombatImpulse(float normalizedStrength)
        {
            if (_reducedMotion)
            {
                return;
            }

            _combatImpulse = Mathf.Max(
                _combatImpulse,
                Mathf.Clamp01(normalizedStrength) * _maximumImpulseDistance);
        }

        /// <summary>
        /// Applies the reduced-motion accessibility preference. Enabling it also
        /// clears an impulse already in progress so the setting takes effect on
        /// the same frame rather than after the previous shake decays.
        /// </summary>
        public void SetReducedMotion(bool reducedMotion)
        {
            _reducedMotion = reducedMotion;
            if (reducedMotion)
            {
                ClearCombatImpulse();
            }
        }

        public void ClearCombatImpulse()
        {
            _combatImpulse = 0f;
        }

        public void SnapToTarget()
        {
            _positionVelocity = Vector3.zero;
            ClearCombatImpulse();
            UpdateCamera(true);
        }

        private void UpdateCamera(bool snap)
        {
            if (_followTarget == null)
            {
                transform.rotation = FixedRotation;
                return;
            }

            Quaternion rotation = FixedRotation;
            Vector3 planarRight = rotation * Vector3.right;
            planarRight.y = 0f;
            if (planarRight.sqrMagnitude > 0.0001f)
            {
                planarRight.Normalize();
            }
            Vector3 focus = _followTarget.position +
                (Vector3.up * _pivotHeight) +
                (PlanarForward * _forwardFocusOffset) +
                (planarRight * _rightFocusOffset);
            Vector3 desiredPosition = focus +
                (rotation * Vector3.back * _followDistance);

            if (_combatImpulse > 0.0001f)
            {
                float phase = Time.unscaledTime * _impulseFrequency;
                desiredPosition += (rotation * Vector3.right) *
                    (Mathf.Sin(phase) * _combatImpulse);
                desiredPosition += Vector3.up *
                    (Mathf.Sin(phase * 1.37f) * _combatImpulse * 0.45f);
                _combatImpulse = Mathf.MoveTowards(
                    _combatImpulse,
                    0f,
                    _impulseDecay * Time.unscaledDeltaTime * _maximumImpulseDistance);
            }

            transform.position = snap || _followSmoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref _positionVelocity,
                    _followSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            transform.rotation = rotation;
        }

        private void ApplyProjection()
        {
            UnityEngine.Camera gameplayCamera = GetComponent<UnityEngine.Camera>();
            gameplayCamera.orthographic = false;
            gameplayCamera.fieldOfView = _fieldOfView;
        }

        private void ApplyPvePresentationSettings()
        {
            if (_pvePresentationApplied) { return; }
            UnityEngine.Camera gameplayCamera = GetComponent<UnityEngine.Camera>();
            _baselineFarClipPlane = gameplayCamera.farClipPlane;
            _baselineClearFlags = gameplayCamera.clearFlags;
            gameplayCamera.farClipPlane = PveLevelingFarClipPlane;
            gameplayCamera.clearFlags = CameraClearFlags.Skybox;
            _pvePresentationApplied = true;
            // The zone lighting presenter owns the sky material and fog. Never
            // overwrite backgroundColor, global RenderSettings or the near plane.
        }

        private void RestorePvePresentationSettings()
        {
            if (!_pvePresentationApplied) { return; }
            UnityEngine.Camera gameplayCamera = GetComponent<UnityEngine.Camera>();
            if (gameplayCamera != null)
            {
                gameplayCamera.farClipPlane = _baselineFarClipPlane;
                gameplayCamera.clearFlags = _baselineClearFlags;
            }
            _pvePresentationApplied = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _yaw = Mathf.Repeat(_yaw, 360f);
            _orbitCenterYaw = Mathf.Repeat(_orbitCenterYaw, 360f);
            _maximumOrbitOffset = Mathf.Clamp(_maximumOrbitOffset, 0f, 180f);
            _pitch = Mathf.Clamp(_pitch,
                _pveLandscapeComposition ? PveLevelingPitch : StrategyMinimumPitch, 80f);
            _followDistance = Mathf.Max(1f, _followDistance);
            _pivotHeight = Mathf.Max(0f, _pivotHeight);
            _forwardFocusOffset = Mathf.Max(0f, _forwardFocusOffset);
            _rightFocusOffset = Mathf.Clamp(_rightFocusOffset, -10f, 10f);
            _fieldOfView = Mathf.Clamp(_fieldOfView, 35f, 70f);
            _followSmoothTime = Mathf.Max(0f, _followSmoothTime);
            _maximumImpulseDistance = Mathf.Max(0f, _maximumImpulseDistance);
            _impulseDecay = Mathf.Max(0.1f, _impulseDecay);
            _impulseFrequency = Mathf.Max(1f, _impulseFrequency);
        }
#endif
    }
}
