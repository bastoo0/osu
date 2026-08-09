// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        private const double direction_change_bonus = 21.0;
        private const double positioning_weight = 0.35;
        private const double path_weight = 0.15;
        private const double timing_exponent = 0.7;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchLast = (CatchDifficultyHitObject)current.Previous(0);
            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double catcherSpeedMultiplier = current.ClockRate;

            double weightedStrainTime = catchCurrent.StrainTime + 13 + (3 / catcherSpeedMultiplier);

            // Physical travel and positioning precision are deliberately separate. Movement inside
            // the catchable interval is not mandatory, but consistently using its very edge leaves
            // little practical error margin and therefore contributes a smaller amount of strain.
            double effectiveDistance = effectiveDistanceOf(catchCurrent);

            double distanceAddition = DiffUtils.Pow(effectiveDistance, 1.3) / 510;
            double sqrtStrain = Math.Sqrt(weightedStrainTime);

            double edgeDashBonus = 0;

            // Direction change bonus.
            if (effectiveDistance > 0.1)
            {
                if (current.Index >= 1)
                {
                    double lastEffectiveDistance = effectiveDistanceOf(catchLast);

                    if (lastEffectiveDistance > 0.1 && catchCurrent.MovementDirection != catchLast.MovementDirection)
                    {
                        double bonusFactor = Math.Min(50, effectiveDistance) / 50;
                        double antiflowFactor = Math.Max(Math.Min(70, lastEffectiveDistance) / 70, 0.38);

                        distanceAddition += direction_change_bonus / Math.Sqrt(catchLast.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - DiffUtils.Pow(weightedStrainTime / 1000, 3), 0);
                    }
                }

                // Base bonus for every movement, giving some weight to streams.
                distanceAddition += 12.5 * Math.Min(effectiveDistance, CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                    / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / sqrtStrain;
            }

            // Predictable same-direction movement needs fewer control changes, but remains a real
            // sustained movement demand. Keep this adjustment bounded rather than exponentially
            // erasing long slider paths and continuous streams.
            int linearSpacingCount = 0;

            for (int i = 0; i < Math.Min(current.Index, 10); i++)
            {
                var catchPrevObj = (CatchDifficultyHitObject)catchCurrent.Previous(i);

                // Only same direction movements matter as they do not take any additional inputs.
                double previousEffectiveDistance = effectiveDistanceOf(catchPrevObj);

                if (catchCurrent.MovementDirection != catchPrevObj.MovementDirection || effectiveDistance == 0 || previousEffectiveDistance == 0)
                    break;

                double currentSpacing = effectiveDistance / catchCurrent.StrainTime;
                double prevSpacing = previousEffectiveDistance / catchPrevObj.StrainTime;

                double relativeDifference = Math.Abs(currentSpacing / prevSpacing - 1);

                if (relativeDifference > 0.05)
                    break;

                linearSpacingCount++;
            }

            distanceAddition *= 0.25 + 0.75 / (1 + linearSpacingCount);

            // Bonus for edge dashes.
            if (catchCurrent.LastObject.DistanceToHyperDash <= 20.0f)
            {
                if (!catchCurrent.LastObject.HyperDash)
                    edgeDashBonus += 5.7;

                distanceAddition *= 1.0 + edgeDashBonus * ((20 - catchCurrent.LastObject.DistanceToHyperDash) / 20)
                                                        * DiffUtils.Pow((Math.Min(catchCurrent.StrainTime * catcherSpeedMultiplier, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
            }

            // Compress timing extremes around 100 ms. Very fast patterns remain harder, but do
            // not grow linearly when the catcher is repeating the same short control cycle.
            double timingNormalisation = DiffUtils.Pow(100, 1 - timing_exponent) * DiffUtils.Pow(weightedStrainTime, timing_exponent);

            return distanceAddition / timingNormalisation;
        }

        private static double effectiveDistanceOf(CatchDifficultyHitObject current)
        {
            double directTravel = current.DirectTravelDistance;

            if (current.Index >= 1)
            {
                var previous = (CatchDifficultyHitObject)current.Previous(0);
                double leftmostPosition = Math.Min(current.NormalizedPosition, Math.Min(current.LastNormalizedPosition, previous.LastNormalizedPosition));
                double rightmostPosition = Math.Max(current.NormalizedPosition, Math.Max(current.LastNormalizedPosition, previous.LastNormalizedPosition));

                // If three consecutive objects share a physical catch interval, the catcher can
                // remain in that intersection. This handles short buzz/wiggle patterns without
                // relying on exact spacing or timing equality.
                if (rightmostPosition - leftmostPosition <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                    directTravel = 0;
            }

            double precisionTravel = Math.Min(directTravel,
                Math.Max(0, current.DirectComfortableTravelDistance - current.DirectTravelDistance));
            double pathTravel = Math.Max(0, current.TravelDistance - current.DirectTravelDistance);

            // Tiny droplets describe the route between combo objects, but are lenient judgements.
            // A cap prevents long or randomised slider paths from becoming one unbounded spike.
            double cappedPathTravel = Math.Min(pathTravel, CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2);

            return directTravel + precisionTravel * positioning_weight + cappedPathTravel * path_weight;
        }
    }
}
