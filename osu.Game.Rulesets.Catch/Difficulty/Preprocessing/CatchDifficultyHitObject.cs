// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing
{
    public class CatchDifficultyHitObject : DifficultyHitObject
    {
        public const float NORMALIZED_HALF_CATCHER_WIDTH = 41.0f;
        private const float absolute_player_positioning_error = 16.0f;
        // Default CS catch width is 85.4 playfield units, or 42.7 per side.
        private const float reference_half_catcher_width = 42.7f;
        private const float playfield_scaling_factor = NORMALIZED_HALF_CATCHER_WIDTH / reference_half_catcher_width;

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
        /// Catchable half-width in the same fixed playfield units as <see cref="NormalizedPosition"/>.
        /// </summary>
        public readonly float NormalizedHalfCatcherWidth;

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
        /// Minimum physical movement needed to enter the next object's catch interval.
        /// </summary>
        public float PhysicalDistanceMoved { get; private set; }

        /// <summary>
        /// Direction of the minimum physical movement.
        /// </summary>
        public int MovementDirection => Math.Sign(PhysicalDistanceMoved);

        private float physicalPlayerPosition;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="CatchDifficultyHitObject"/>, with a minimum of 40ms.
        /// </summary>
        public readonly double StrainTime;

        public CatchDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, float halfCatcherWidth, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            // Keep physical travel in fixed playfield units. Circle size changes the interval in
            // which an object can be caught, but does not change the catcher's movement speed.
            NormalizedPosition = BaseObject.EffectiveX * playfield_scaling_factor;
            LastNormalizedPosition = LastObject.EffectiveX * playfield_scaling_factor;
            NormalizedHalfCatcherWidth = halfCatcherWidth * playfield_scaling_factor;

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(40, DeltaTime);

            setMovementState();
        }

        private void setMovementState()
        {
            LastPlayerPosition = Index == 0 ? LastNormalizedPosition : ((CatchDifficultyHitObject)Previous(0)).PlayerPosition;
            float lastPhysicalPlayerPosition = Index == 0
                ? LastNormalizedPosition
                : ((CatchDifficultyHitObject)Previous(0)).physicalPlayerPosition;

            PlayerPosition = Math.Clamp(
                LastPlayerPosition,
                NormalizedPosition - Math.Max(0, NormalizedHalfCatcherWidth - absolute_player_positioning_error),
                NormalizedPosition + Math.Max(0, NormalizedHalfCatcherWidth - absolute_player_positioning_error)
            );

            DistanceMoved = PlayerPosition - LastPlayerPosition;

            physicalPlayerPosition = Math.Clamp(
                lastPhysicalPlayerPosition,
                NormalizedPosition - NormalizedHalfCatcherWidth,
                NormalizedPosition + NormalizedHalfCatcherWidth
            );
            PhysicalDistanceMoved = physicalPlayerPosition - lastPhysicalPlayerPosition;

            // For the exact position we consider that the catcher is in the correct position for both objects
            ExactDistanceMoved = NormalizedPosition - LastPlayerPosition;

            // After a hyperdash we ARE in the correct position. Always!
            if (LastObject.HyperDash)
            {
                PlayerPosition = NormalizedPosition;
                physicalPlayerPosition = NormalizedPosition;
            }
        }
    }
}
