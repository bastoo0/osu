// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        public const float NORMALIZED_HALF_CATCHER_WIDTH = 41.0f;
        private const float absolute_player_positioning_error = 16.0f;

        private const float comfortable_catch_range = NORMALIZED_HALF_CATCHER_WIDTH - absolute_player_positioning_error;

        public new PalpableCatchHitObject BaseObject => (PalpableCatchHitObject)base.BaseObject;

        public new PalpableCatchHitObject LastObject => (PalpableCatchHitObject)base.LastObject;

        /// <summary>
        /// Normalized position of <see cref="BaseObject"/>.
        /// </summary>
        public readonly float NormalizedPosition;

        /// <summary>
        /// Normalized position of <see cref="LastObject"/>.
        /// </summary>
        public readonly float LastNormalizedPosition;

        /// <summary>
        /// Normalized position of the player required to catch <see cref="BaseObject"/>, assuming the player moves as little as possible.
        /// </summary>
        public float PlayerPosition { get; private set; }

        /// <summary>
        /// Normalized position of the player after catching <see cref="LastObject"/>.
        /// </summary>
        public float LastPlayerPosition { get; private set; }

        /// <summary>
        /// Normalized distance between <see cref="LastPlayerPosition"/> and <see cref="PlayerPosition"/>.
        /// </summary>
        /// <remarks>
        /// The sign of the value indicates the direction of the movement: negative is left and positive is right.
        /// </remarks>
        public float DistanceMoved { get; private set; }

        /// <summary>
        /// Normalized distance the player has to move from <see cref="LastPlayerPosition"/> in order to catch <see cref="BaseObject"/> at its <see cref="NormalizedPosition"/>.
        /// </summary>
        /// <remarks>
        /// The sign of the value indicates the direction of the movement: negative is left and positive is right.
        /// </remarks>
        public float ExactDistanceMoved { get; private set; }

        /// <summary>
        /// Total distance which must be travelled through the physical catch intervals between
        /// <see cref="LastObject"/> and <see cref="BaseObject"/>.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="DistanceMoved"/>, this includes detours required by tiny droplets.
        /// </remarks>
        public float TravelDistance { get; private set; }

        /// <summary>
        /// Total distance required when keeping a consistent positioning margin from the edge of
        /// the catcher's physical catch interval.
        /// </summary>
        public float ComfortableTravelDistance { get; private set; }

        /// <summary>
        /// Minimum physical travel directly between the two combo objects, without treating tiny
        /// droplets as mandatory waypoints.
        /// </summary>
        public float DirectTravelDistance { get; private set; }

        /// <summary>
        /// Direct travel required while retaining the comfortable positioning margin.
        /// </summary>
        public float DirectComfortableTravelDistance { get; private set; }

        /// <summary>
        /// Direction of the final required movement in the path ending at <see cref="BaseObject"/>.
        /// </summary>
        public int MovementDirection { get; private set; }

        /// <summary>
        /// Number of tiny droplets which constrain the path from <see cref="LastObject"/> to
        /// <see cref="BaseObject"/>.
        /// </summary>
        public int PathObjectCount { get; }

        private float comfortablePlayerPosition;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="CatchDifficultyHitObject"/>, with a minimum of 40ms.
        /// </summary>
        public readonly double StrainTime;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, float halfCatcherWidth, List<DifficultyHitObject> objects, int index,
                                        IReadOnlyList<TinyDroplet>? pathObjects = null)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            // We will scale everything by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALIZED_HALF_CATCHER_WIDTH / halfCatcherWidth;

            NormalizedPosition = BaseObject.EffectiveX * scalingFactor;
            LastNormalizedPosition = LastObject.EffectiveX * scalingFactor;

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(40, DeltaTime);

            PathObjectCount = pathObjects?.Count ?? 0;

            setMovementState(scalingFactor, pathObjects ?? Array.Empty<TinyDroplet>());
        }

        private void setMovementState(float scalingFactor, IReadOnlyList<TinyDroplet> pathObjects)
        {
            LastPlayerPosition = Index == 0 ? LastNormalizedPosition : ((CatchDifficultyHitObject)Previous(0)).PlayerPosition;
            float lastComfortablePlayerPosition = Index == 0 ? LastNormalizedPosition : ((CatchDifficultyHitObject)Previous(0)).comfortablePlayerPosition;

            float playerPosition = LastPlayerPosition;
            comfortablePlayerPosition = lastComfortablePlayerPosition;

            int lastMovementDirection = 0;
            int directMovementDirection = 0;
            float directPlayerPosition = LastPlayerPosition;
            float directComfortablePlayerPosition = lastComfortablePlayerPosition;

            DirectTravelDistance = moveToCatchInterval(ref directPlayerPosition, NormalizedPosition, NORMALIZED_HALF_CATCHER_WIDTH, ref directMovementDirection);
            DirectComfortableTravelDistance = moveToCatchInterval(ref directComfortablePlayerPosition, NormalizedPosition, comfortable_catch_range, ref directMovementDirection);

            // Tiny droplets are lenient score objects, but they describe the path which the catcher
            // follows between combo objects. Traverse their catch intervals without giving each one
            // an independent strain peak.
            if (!LastObject.HyperDash)
            {
                foreach (float position in pathObjects.Select(pathObject => pathObject.EffectiveX * scalingFactor).Append(NormalizedPosition))
                {
                    TravelDistance += moveToCatchInterval(ref playerPosition, position, NORMALIZED_HALF_CATCHER_WIDTH, ref lastMovementDirection);
                    ComfortableTravelDistance += moveToCatchInterval(ref comfortablePlayerPosition, position, comfortable_catch_range, ref lastMovementDirection);
                }

                MovementDirection = lastMovementDirection;
            }
            else
            {
                // Hyperdashes target the next combo object and pass through any tiny droplets along
                // the way. Measure the movement demand at the target, then use the exact landing
                // position as the starting state for the following pattern.
                TravelDistance = DirectTravelDistance;
                ComfortableTravelDistance = DirectComfortableTravelDistance;
                MovementDirection = directMovementDirection;

                playerPosition = NormalizedPosition;
                comfortablePlayerPosition = NormalizedPosition;
            }

            PlayerPosition = playerPosition;
            DistanceMoved = PlayerPosition - LastPlayerPosition;

            // The exact position is retained for diagnostics and compatibility comparisons.
            ExactDistanceMoved = NormalizedPosition - LastPlayerPosition;
        }

        private static float moveToCatchInterval(ref float playerPosition, float objectPosition, float catchRange, ref int lastMovementDirection)
        {
            float nextPlayerPosition = Math.Clamp(playerPosition, objectPosition - catchRange, objectPosition + catchRange);
            float distance = nextPlayerPosition - playerPosition;

            if (Math.Abs(distance) > 0.1f)
                lastMovementDirection = Math.Sign(distance);

            playerPosition = nextPlayerPosition;
            return Math.Abs(distance);
        }
    }
}
