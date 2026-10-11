using System;

namespace SessionRpg.Core
{
    public readonly struct ArenaCircleObstacle
    {
        public ArenaCircleObstacle(Float3 center, float radius)
        {
            Center = center;
            Radius = Math.Max(0f, radius);
        }

        public Float3 Center { get; }

        public float Radius { get; }
    }

    /// <summary>
    /// Allocation-free planar collision shared by authoritative combat paths.
    /// Arena uses two circular blockers; Training uses a bounded rectangular lane;
    /// persistent PvE uses a two-dimensional bounded region with solid islands.
    /// Movement, displacement, teleports, line of sight, and
    /// bots therefore agree without consulting Unity physics.
    /// </summary>
    public readonly struct ArenaCollisionMap
    {
        public const int ArenaObstacleCount = 2;
        public const int SiegeObstacleCount = 1;
        public const float SiegeCoreRadius = 1.65f;
        public const float SiegeMaximumRadius = 15f;
        public const float LevelingMinimumX = -191f;
        public const float LevelingMaximumX = 255f;
        public const float LevelingCenterZ = 32f;
        public const float LevelingHalfWidth = 7.55f;

        private const float ContactSkin = 0.0025f;
        private const float GeometryEpsilon = 0.00001f;
        private const int MaximumSlideIterations = 3;
        private const byte ArenaKind = 1;
        private const byte LevelingKind = 2;
        private const byte SiegeKind = 3;
        private const byte PveLevelingKind = 4;

        private readonly byte _kind;

        private ArenaCollisionMap(byte kind)
        {
            _kind = kind;
        }

        public bool IsEnabled => _kind != 0;

        public int ObstacleCount => _kind == ArenaKind
            ? ArenaObstacleCount
            : _kind == SiegeKind
                ? SiegeObstacleCount
                : _kind == PveLevelingKind
                    ? PveRegionLayout.ObstacleCount
                    : 0;

        public static ArenaCollisionMap None => default;

        public static ArenaCollisionMap ArenaDefault =>
            new ArenaCollisionMap(ArenaKind);

        /// <summary>
        /// Core-owned walkable envelope for the first streamed west-to-east zone.
        /// Presentation keeps walls, cinder, and lava outside this conservative
        /// lane so they never masquerade as Unity-physics gameplay geometry.
        /// </summary>
        public static ArenaCollisionMap LevelingDefault =>
            new ArenaCollisionMap(LevelingKind);

        /// <summary>
        /// Four unobstructed cardinal approaches around one authoritative central
        /// stronghold footprint. The objective is not a combatant, but movement,
        /// displacement, teleports, targeting, and bots all respect its footprint.
        /// </summary>
        public static ArenaCollisionMap SiegeDefault =>
            new ArenaCollisionMap(SiegeKind);

        /// <summary>
        /// A full two-dimensional region with an inset rectangle and solid,
        /// line-of-sight-blocking islands. Training's narrow lane is unchanged.
        /// </summary>
        public static ArenaCollisionMap PveLevelingDefault =>
            new ArenaCollisionMap(PveLevelingKind);

        public ArenaCircleObstacle GetObstacle(int index)
        {
            if (index < 0 || index >= ObstacleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_kind == SiegeKind)
            {
                return new ArenaCircleObstacle(default, SiegeCoreRadius);
            }

            if (_kind == PveLevelingKind)
            {
                return PveRegionLayout.GetObstacle(index);
            }

            float z = index == 0
                ? -ArenaLayout.PillarOffsetZ
                : ArenaLayout.PillarOffsetZ;
            return new ArenaCircleObstacle(
                new Float3(0f, 0f, z),
                ArenaLayout.PillarRadius);
        }

        public bool IsPositionValid(Float3 position, float actorRadius)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (_kind == LevelingKind &&
                !IsInsideLevelingBounds(position, actorRadius))
            {
                return false;
            }

            if (_kind == PveLevelingKind &&
                !IsInsidePveBounds(position, actorRadius))
            {
                return false;
            }

            if (_kind == SiegeKind &&
                position.PlanarLength >
                    SiegeMaximumRadius - Math.Max(0f, actorRadius) +
                    GeometryEpsilon)
            {
                return false;
            }

            float clearance = Math.Max(0f, actorRadius);
            for (int index = 0; index < ObstacleCount; index++)
            {
                ArenaCircleObstacle obstacle = GetObstacle(index);
                float radius = obstacle.Radius + clearance;
                if ((position - obstacle.Center).PlanarLengthSquared <
                    radius * radius)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasLineOfSight(Float3 origin, Float3 target)
        {
            return !TryGetFirstBlockingObstacle(origin, target, out _);
        }

        public bool TryGetFirstBlockingObstacle(
            Float3 origin,
            Float3 target,
            out ArenaCircleObstacle obstacle)
        {
            obstacle = default;
            if (!IsEnabled)
            {
                return false;
            }

            Float3 delta = target - origin;
            float lengthSquared = delta.PlanarLengthSquared;
            if (lengthSquared <= GeometryEpsilon)
            {
                return false;
            }

            bool found = false;
            float earliestFraction = float.MaxValue;
            for (int index = 0; index < ObstacleCount; index++)
            {
                ArenaCircleObstacle candidate = GetObstacle(index);
                if (!TryGetSegmentCircleFraction(
                        origin,
                        delta,
                        candidate.Center,
                        candidate.Radius,
                        out float fraction) ||
                    fraction >= earliestFraction)
                {
                    continue;
                }

                earliestFraction = fraction;
                obstacle = candidate;
                found = true;
            }

            return found;
        }

        public Float3 ResolveMovement(
            Float3 origin,
            Float3 displacement,
            float actorRadius)
        {
            return ResolveSweep(origin, displacement, actorRadius, true);
        }

        public Float3 ResolveKnockback(
            Float3 origin,
            Float3 displacement,
            float actorRadius)
        {
            return ResolveSweep(origin, displacement, actorRadius, false);
        }

        public Float3 ResolveBlink(
            Float3 origin,
            Float3 desiredPosition,
            float actorRadius)
        {
            return ResolveSweep(
                origin,
                desiredPosition - origin,
                actorRadius,
                false);
        }

        public Float3 ResolvePlacement(
            Float3 desiredPosition,
            float actorRadius,
            Float3 preferredDirection)
        {
            if (!IsEnabled)
            {
                return Planar(desiredPosition);
            }

            Float3 resolved = Planar(desiredPosition);
            if (_kind == LevelingKind)
            {
                return ClampToLevelingBounds(resolved, actorRadius);
            }

            if (_kind == SiegeKind)
            {
                resolved = ClampToSiegeBounds(resolved, actorRadius);
            }
            else if (_kind == PveLevelingKind)
            {
                resolved = ClampToPveBounds(resolved, actorRadius);
            }

            float clearance = Math.Max(0f, actorRadius) + ContactSkin;
            for (int iteration = 0; iteration < ObstacleCount; iteration++)
            {
                bool projected = false;
                for (int index = 0; index < ObstacleCount; index++)
                {
                    ArenaCircleObstacle obstacle = GetObstacle(index);
                    float radius = obstacle.Radius + clearance;
                    Float3 offset = resolved - obstacle.Center;
                    if (offset.PlanarLengthSquared >= radius * radius)
                    {
                        continue;
                    }

                    Float3 direction = offset.PlanarLengthSquared > GeometryEpsilon
                        ? offset.PlanarNormalized
                        : preferredDirection.PlanarLengthSquared > GeometryEpsilon
                            ? preferredDirection.PlanarNormalized
                            : new Float3(1f, 0f, 0f);
                    resolved = obstacle.Center + (direction * radius);
                    projected = true;
                }

                // Most warmed gameplay queries are already outside all cover.
                // Union components only require another pass when a projection
                // could have entered a component visited earlier in this pass.
                if (!projected)
                {
                    break;
                }
            }

            return _kind == PveLevelingKind
                ? ClampToPveBounds(resolved, actorRadius)
                : resolved;
        }

        private static Float3 ClampToSiegeBounds(
            Float3 position,
            float actorRadius)
        {
            float maximumCenterRadius = Math.Max(
                0f,
                SiegeMaximumRadius - Math.Max(0f, actorRadius) - ContactSkin);
            if (position.PlanarLength <= maximumCenterRadius)
            {
                return position;
            }

            return position.PlanarNormalized * maximumCenterRadius;
        }

        public bool TryResolveShadowstep(
            Float3 targetPosition,
            Float3 targetFacing,
            float separation,
            float actorRadius,
            out Float3 resolvedPosition)
        {
            Float3 facing = targetFacing.PlanarNormalized;
            Float3 behind = targetPosition - (facing * separation);
            if (!IsEnabled || IsPositionValid(behind, actorRadius))
            {
                resolvedPosition = Planar(behind);
                return true;
            }

            Float3 tangent = new Float3(-facing.Z, 0f, facing.X);
            Float3 firstSide = targetPosition + (tangent * separation);
            if (IsPositionValid(firstSide, actorRadius))
            {
                resolvedPosition = Planar(firstSide);
                return true;
            }

            Float3 secondSide = targetPosition - (tangent * separation);
            if (IsPositionValid(secondSide, actorRadius))
            {
                resolvedPosition = Planar(secondSide);
                return true;
            }

            resolvedPosition = ResolvePlacement(
                behind,
                actorRadius,
                default(Float3) - facing);
            return IsPositionValid(resolvedPosition, actorRadius);
        }

        public Float3 BuildDetourDirection(
            Float3 origin,
            Float3 target,
            bool clockwise)
        {
            if (!TryGetFirstBlockingObstacle(origin, target, out ArenaCircleObstacle obstacle))
            {
                return (target - origin).PlanarNormalized;
            }

            Float3 outward = origin - obstacle.Center;
            if (outward.PlanarLengthSquared <= GeometryEpsilon)
            {
                outward = new Float3(1f, 0f, 0f);
            }

            outward = outward.PlanarNormalized;
            Float3 tangent = clockwise
                ? new Float3(outward.Z, 0f, -outward.X)
                : new Float3(-outward.Z, 0f, outward.X);
            return (tangent + (outward * 0.24f)).PlanarNormalized;
        }

        private Float3 ResolveSweep(
            Float3 origin,
            Float3 displacement,
            float actorRadius,
            bool allowSlide)
        {
            Float3 planarOrigin = Planar(origin);
            Float3 planarDisplacement = Planar(displacement);
            if (!IsEnabled || planarDisplacement.PlanarLengthSquared <= GeometryEpsilon)
            {
                return planarOrigin + planarDisplacement;
            }

            if (_kind == LevelingKind)
            {
                return ResolvePlacement(
                    planarOrigin + planarDisplacement,
                    actorRadius,
                    default(Float3) - planarDisplacement);
            }

            Float3 current = ResolvePlacement(
                planarOrigin,
                actorRadius,
                default(Float3) - planarDisplacement);
            Float3 remaining = planarDisplacement;
            int iterations = allowSlide ? MaximumSlideIterations : 1;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (remaining.PlanarLengthSquared <= GeometryEpsilon)
                {
                    break;
                }

                if (!TryGetEarliestSweepHit(
                        current,
                        remaining,
                        actorRadius,
                        out float hitFraction,
                        out Float3 hitNormal))
                {
                    current += remaining;
                    remaining = default;
                    break;
                }

                float distance = remaining.PlanarLength;
                float skinFraction = distance > GeometryEpsilon
                    ? ContactSkin / distance
                    : 0f;
                float travelFraction = Math.Max(0f, hitFraction - skinFraction);
                current += remaining * travelFraction;
                if (!allowSlide)
                {
                    remaining = default;
                    break;
                }

                Float3 afterHit = remaining * (1f - hitFraction);
                float inward = Dot(afterHit, hitNormal);
                remaining = inward < 0f
                    ? afterHit - (hitNormal * inward)
                    : afterHit;
            }

            return ResolvePlacement(current, actorRadius, default(Float3) - planarDisplacement);
        }

        private bool TryGetEarliestSweepHit(
            Float3 origin,
            Float3 displacement,
            float actorRadius,
            out float hitFraction,
            out Float3 hitNormal)
        {
            hitFraction = float.MaxValue;
            hitNormal = default;
            bool found = false;
            float clearance = Math.Max(0f, actorRadius);
            for (int index = 0; index < ObstacleCount; index++)
            {
                ArenaCircleObstacle obstacle = GetObstacle(index);
                if (!TryGetSweepFraction(
                        origin,
                        displacement,
                        obstacle.Center,
                        obstacle.Radius + clearance,
                        out float fraction) ||
                    fraction >= hitFraction)
                {
                    continue;
                }

                Float3 contact = origin + (displacement * fraction);
                Float3 normal = contact - obstacle.Center;
                hitNormal = normal.PlanarLengthSquared > GeometryEpsilon
                    ? normal.PlanarNormalized
                    : (default(Float3) - displacement).PlanarNormalized;
                hitFraction = fraction;
                found = true;
            }

            if (_kind == PveLevelingKind)
            {
                // Treat the region edges as sweep contacts too: movement may
                // slide along them, while Blink and knockback stop at the first
                // contact rather than clamping a diagonal destination sideways.
                if (displacement.X < -GeometryEpsilon)
                {
                    found |= ConsiderBoundsHit(
                        (PveRegionLayout.MinimumX + clearance - origin.X) / displacement.X,
                        new Float3(1f, 0f, 0f), ref hitFraction, ref hitNormal);
                }
                else if (displacement.X > GeometryEpsilon)
                {
                    found |= ConsiderBoundsHit(
                        (PveRegionLayout.MaximumX - clearance - origin.X) / displacement.X,
                        new Float3(-1f, 0f, 0f), ref hitFraction, ref hitNormal);
                }

                if (displacement.Z < -GeometryEpsilon)
                {
                    found |= ConsiderBoundsHit(
                        (PveRegionLayout.MinimumZ + clearance - origin.Z) / displacement.Z,
                        new Float3(0f, 0f, 1f), ref hitFraction, ref hitNormal);
                }
                else if (displacement.Z > GeometryEpsilon)
                {
                    found |= ConsiderBoundsHit(
                        (PveRegionLayout.MaximumZ - clearance - origin.Z) / displacement.Z,
                        new Float3(0f, 0f, -1f), ref hitFraction, ref hitNormal);
                }
            }

            return found;
        }

        private static bool ConsiderBoundsHit(
            float fraction,
            Float3 normal,
            ref float hitFraction,
            ref Float3 hitNormal)
        {
            if (fraction < 0f || fraction > 1f || fraction >= hitFraction)
            {
                return false;
            }

            hitFraction = fraction;
            hitNormal = normal;
            return true;
        }

        private static bool TryGetSegmentCircleFraction(
            Float3 origin,
            Float3 displacement,
            Float3 center,
            float radius,
            out float fraction)
        {
            Float3 offset = origin - center;
            float lengthSquared = displacement.PlanarLengthSquared;
            float closestFraction = -Dot(offset, displacement) / lengthSquared;
            closestFraction = Math.Max(0f, Math.Min(1f, closestFraction));
            Float3 closest = origin + (displacement * closestFraction);
            float radiusSquared = radius * radius;
            float closestDistanceSquared = (closest - center).PlanarLengthSquared;
            float contactTolerance = GeometryEpsilon * Math.Max(1f, radiusSquared);
            if (closestDistanceSquared > radiusSquared + contactTolerance)
            {
                fraction = 0f;
                return false;
            }

            return TryGetSweepFraction(
                origin,
                displacement,
                center,
                radius,
                out fraction);
        }

        private static bool TryGetSweepFraction(
            Float3 origin,
            Float3 displacement,
            Float3 center,
            float radius,
            out float fraction)
        {
            Float3 offset = origin - center;
            float a = displacement.PlanarLengthSquared;
            if (a <= GeometryEpsilon)
            {
                fraction = 0f;
                return false;
            }

            float b = Dot(offset, displacement);
            float c = offset.PlanarLengthSquared - (radius * radius);
            if (c <= 0f)
            {
                if (b >= 0f)
                {
                    fraction = 0f;
                    return false;
                }

                fraction = 0f;
                return true;
            }

            if (b >= 0f)
            {
                fraction = 0f;
                return false;
            }

            float discriminant = (b * b) - (a * c);
            float discriminantTolerance = GeometryEpsilon * Math.Max(
                1f,
                (b * b) + Math.Abs(a * c));
            if (discriminant < -discriminantTolerance)
            {
                fraction = 0f;
                return false;
            }

            discriminant = Math.Max(0f, discriminant);
            float candidate = (-b - (float)Math.Sqrt(discriminant)) / a;
            if (candidate < 0f || candidate > 1f)
            {
                fraction = 0f;
                return false;
            }

            fraction = candidate;
            return true;
        }

        private static float Dot(Float3 left, Float3 right)
        {
            return (left.X * right.X) + (left.Z * right.Z);
        }

        private static Float3 Planar(Float3 value)
        {
            return new Float3(value.X, 0f, value.Z);
        }

        private static bool IsInsideLevelingBounds(
            Float3 position,
            float actorRadius)
        {
            // Validation accepts the contact skin authored by resolution. The
            // same convention is used for arena-circle validity above.
            float clearance = Math.Max(0f, actorRadius);
            return position.X >= LevelingMinimumX + clearance &&
                position.X <= LevelingMaximumX - clearance &&
                position.Z >= LevelingCenterZ - LevelingHalfWidth + clearance &&
                position.Z <= LevelingCenterZ + LevelingHalfWidth - clearance;
        }

        private static bool IsInsidePveBounds(Float3 position, float actorRadius)
        {
            float clearance = Math.Max(0f, actorRadius);
            return position.X >= PveRegionLayout.MinimumX + clearance &&
                position.X <= PveRegionLayout.MaximumX - clearance &&
                position.Z >= PveRegionLayout.MinimumZ + clearance &&
                position.Z <= PveRegionLayout.MaximumZ - clearance;
        }

        private static Float3 ClampToPveBounds(Float3 position, float actorRadius)
        {
            float clearance = Math.Max(0f, actorRadius) + ContactSkin;
            return new Float3(
                Math.Max(PveRegionLayout.MinimumX + clearance,
                    Math.Min(PveRegionLayout.MaximumX - clearance, position.X)),
                0f,
                Math.Max(PveRegionLayout.MinimumZ + clearance,
                    Math.Min(PveRegionLayout.MaximumZ - clearance, position.Z)));
        }

        private static Float3 ClampToLevelingBounds(
            Float3 position,
            float actorRadius)
        {
            float clearance = Math.Max(0f, actorRadius) + ContactSkin;
            float minimumX = LevelingMinimumX + clearance;
            float maximumX = LevelingMaximumX - clearance;
            float minimumZ = LevelingCenterZ - LevelingHalfWidth + clearance;
            float maximumZ = LevelingCenterZ + LevelingHalfWidth - clearance;
            return new Float3(
                Math.Max(minimumX, Math.Min(maximumX, position.X)),
                0f,
                Math.Max(minimumZ, Math.Min(maximumZ, position.Z)));
        }
    }
}
