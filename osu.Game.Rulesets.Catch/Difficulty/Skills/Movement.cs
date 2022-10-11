// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : StrainDecaySkill
    {
        private const float absolute_player_positioning_error = 12f;
        private const float normalized_hitobject_radius = 41.0f;

        protected override double SkillMultiplier => 610;
        protected override double StrainDecayBase => 0.2;

        protected override double DecayWeight => 0.94;

        protected override int SectionLength => 750;

        protected readonly float HalfCatcherWidth;

        private float? lastPlayerPosition;
        private float lastDistanceMoved;
        private double lastStrainTime;
        private bool isTapDashBoosted = false;
        private int tapDashDirection;
        private double tapDashStrainTimeAddition;
        private double tapDashDistanceAddition;
        private double lastExactDistanceMoved;
        private bool previousLastObjectWasHyperDash;
        private bool isBuzzSliderTriggered = false;

        public int DirectionChangeCount;

        /// <summary>
        /// The speed multiplier applied to the player's catcher.
        /// </summary>
        private readonly double catcherSpeedMultiplier;

        public Movement(Mod[] mods, float halfCatcherWidth, double clockRate)
            : base(mods)
        {
            HalfCatcherWidth = halfCatcherWidth;

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            // TODO: Support variable clockrates caused by mods such as ModTimeRamp
            //  (perhaps by using IApplicableToRate within the CatchDifficultyHitObject constructor to set a catcher speed for each object before processing)
            catcherSpeedMultiplier = clockRate;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            lastPlayerPosition ??= catchCurrent.LastNormalizedPosition;

            float playerPosition = Math.Clamp(
                lastPlayerPosition.Value,
                catchCurrent.NormalizedPosition - (normalized_hitobject_radius - absolute_player_positioning_error),
                catchCurrent.NormalizedPosition + (normalized_hitobject_radius - absolute_player_positioning_error)
            );


            float distanceMoved = playerPosition - lastPlayerPosition.Value;
            double exactDistanceMoved = catchCurrent.NormalizedPosition - lastPlayerPosition.Value;

            double weightedStrainTime = catchCurrent.StrainTime / Math.Pow(catcherSpeedMultiplier, 0.15);
            double edgeDashBonus = 0;
            bool isSameDirection = Math.Sign(exactDistanceMoved) == Math.Sign(lastExactDistanceMoved);

            double movementValue;

            // Counting direction changes for length scaling in the performance calculation
            if (Math.Sign(distanceMoved) != Math.Sign(lastDistanceMoved) && Math.Sign(lastDistanceMoved) != 0 && Math.Abs(distanceMoved) > absolute_player_positioning_error)
            {
                DirectionChangeCount += 1;
            }

            // Separating HyperDashes and basic fruits calculations
            // Basic fruit calculation
            if (!catchCurrent.LastObject.HyperDash)
            {
                // The base value is a ratio between distance moved and strain time
                movementValue = 0.125 * Math.Pow(Math.Pow(Math.Abs(distanceMoved), 0.76) / weightedStrainTime, 1.3);

                if (Math.Abs(distanceMoved) > 0.1 && Math.Sign(distanceMoved) != Math.Sign(lastDistanceMoved) && Math.Sign(lastDistanceMoved) != 0)
                {
                    // We buff shorter movements upon direction change
                    movementValue *= 1.2 + (40 / Math.Pow(Math.Abs(exactDistanceMoved), 0.7));
                }
                else movementValue *= 0.65;
            }
            else
            {
                // Hyperdashes calculation
                // Both strain time and distance moved are scaled down because both factors are not optimally representing the difficulty
                movementValue = 0.092 * Math.Pow(Math.Abs(distanceMoved) / (weightedStrainTime), 0.5);

                // Scaling hyperdash chains according to movement
                movementValue *= isSameDirection ? Math.Pow(Math.Abs(distanceMoved), 0.5) / 36 : 1.29 / Math.Pow(Math.Abs(distanceMoved), 0.2);
            }

            // We handle the case of tap-dashes ending with hdashes (stacks that require tapping in the same direction)
            if (catchCurrent.LastObject.HyperDash && Math.Abs(distanceMoved) > HalfCatcherWidth && isTapDashBoosted)
            {
                if (tapDashDirection == Math.Sign(distanceMoved))
                {
                    // Scaling factor is used to calculate the value using time spent since last "tap"
                    double tapDashScalingFactor = Math.Log(tapDashStrainTimeAddition - 33, 1.3) - 0.07 * tapDashStrainTimeAddition - 7.7;
                    // Bonus is nerfed when the stack is not straight
                    double tapDashBonus = tapDashScalingFactor * Math.Max(0, -0.03 * Math.Abs(tapDashDistanceAddition) + 1);
                    movementValue *= 1 + Math.Max(0, tapDashBonus);
                    isTapDashBoosted = false;
                }
                else isTapDashBoosted = false;
            }

            if (previousLastObjectWasHyperDash && Math.Abs(lastDistanceMoved) > 0 && distanceMoved == 0)
            {
                tapDashDirection = Math.Sign(lastDistanceMoved);
                tapDashStrainTimeAddition = 0;
                tapDashDistanceAddition = 0;
                isTapDashBoosted = true;
            }

            if (isTapDashBoosted)
            {
                tapDashStrainTimeAddition += weightedStrainTime;
                tapDashDistanceAddition += exactDistanceMoved;
            }

            // Edge dashes buff
            if (catchCurrent.LastObject.DistanceToHyperDash <= 20)
            {
                if (!catchCurrent.LastObject.HyperDash)
                    edgeDashBonus += 10;
                else
                {
                    // After a hyperdash we ARE in the correct position. Always!
                    playerPosition = catchCurrent.NormalizedPosition;
                }

                // The edge dash bonus is scaled according to the distance to hyperdashes
                movementValue *= 1.0 + edgeDashBonus * ((20 - catchCurrent.LastObject.DistanceToHyperDash) / 45) * Math.Pow(Math.Min(catchCurrent.StrainTime, 265) / 265, 2); // Edge Dashes are easier at lower ms values
            }

            if (Math.Abs(distanceMoved) <= absolute_player_positioning_error && Math.Abs(exactDistanceMoved) == Math.Abs(lastExactDistanceMoved) && catchCurrent.StrainTime == lastStrainTime)
            {
                if (isBuzzSliderTriggered) movementValue = 0;
                else isBuzzSliderTriggered = true;
            }
            else if (isBuzzSliderTriggered) isBuzzSliderTriggered = false;

            lastPlayerPosition = playerPosition;
            lastDistanceMoved = distanceMoved;
            lastStrainTime = catchCurrent.StrainTime;
            lastExactDistanceMoved = exactDistanceMoved;
            previousLastObjectWasHyperDash = catchCurrent.LastObject.HyperDash;

            return movementValue;
        }
    }
}
