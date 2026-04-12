// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        private const double direction_change_bonus = 8.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchLast = (CatchDifficultyHitObject)current.Previous(0);
            var catchLastLast = (CatchDifficultyHitObject)current.Previous(1);

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double catcherSpeedMultiplier = current.ClockRate;

            double weightedStrainTime = catchCurrent.StrainTime + 13 + (3 / catcherSpeedMultiplier);

            double distanceAddition = (Math.Pow(Math.Abs(catchCurrent.DistanceMoved), 1.15) / 510);
            double sqrtStrain = Math.Sqrt(weightedStrainTime);

            double edgeDashBonus = 0;

            // Direction change bonus.
            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1)
            {
                if (current.Index >= 1 && Math.Abs(catchLast.DistanceMoved) > 0.1 && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
                {
                    double bonusFactor = Math.Min(50, Math.Abs(catchCurrent.DistanceMoved)) / 50;
                    double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(catchLast.DistanceMoved)) / 70, 0.38);

                    distanceAddition += direction_change_bonus / Math.Sqrt(catchLast.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
                }

                // Base bonus for every movement, giving some weight to streams.
                distanceAddition += 25.0 * Math.Min(Math.Abs(catchCurrent.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                    / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / sqrtStrain;
            }

            if (current.Index >= 2
                && Math.Abs(catchCurrent.DistanceMoved) > 0.1
                && Math.Abs(catchLast.DistanceMoved) > 0.1
                && Math.Abs(catchLastLast.DistanceMoved) > 0.1
                && Math.Sign(catchCurrent.DistanceMoved) == Math.Sign(catchLastLast.DistanceMoved)
                && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
            {
                double reversalDistance = (Math.Abs(catchCurrent.DistanceMoved) + Math.Abs(catchLast.DistanceMoved) + Math.Abs(catchLastLast.DistanceMoved))
                                          / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 3);

                distanceAddition += 4.2 * Math.Pow(reversalDistance, 1.15)
                                    / Math.Sqrt(catchLastLast.StrainTime + 20)
                                    * Math.Max(0.35, 1 - weightedStrainTime / 900);
            }

            if (current.Index >= 2
                && Math.Abs(catchCurrent.DistanceMoved) > 0.1
                && Math.Abs(catchLast.DistanceMoved) > 0.1
                && Math.Abs(catchLastLast.DistanceMoved) > 0.1
                && Math.Sign(catchCurrent.DistanceMoved) == Math.Sign(catchLast.DistanceMoved)
                && Math.Sign(catchCurrent.DistanceMoved) == Math.Sign(catchLastLast.DistanceMoved))
            {
                double travelPressure = Math.Max(0, Math.Abs(catchCurrent.ExactDistanceMoved) - Math.Abs(catchCurrent.DistanceMoved))
                                       + Math.Max(0, Math.Abs(catchLast.ExactDistanceMoved) - Math.Abs(catchLast.DistanceMoved))
                                       + Math.Max(0, Math.Abs(catchLastLast.ExactDistanceMoved) - Math.Abs(catchLastLast.DistanceMoved));

                if (travelPressure > CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 0.8)
                {
                    double sweepDistance = (Math.Abs(catchCurrent.DistanceMoved) + Math.Abs(catchLast.DistanceMoved) + Math.Abs(catchLastLast.DistanceMoved))
                                           / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 3);
                    double normalizedTravelPressure = travelPressure / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2.4);

                    distanceAddition += 3.6 * Math.Pow(normalizedTravelPressure, 1.1)
                                        * Math.Pow(sweepDistance, 0.8)
                                        / Math.Sqrt(catchLastLast.StrainTime + 24)
                                        * Math.Max(0.25, 1 - (weightedStrainTime + catchLast.StrainTime) / 1050);
                }
            }

            // Bonus for edge dashes.
            if (catchCurrent.LastObject.DistanceToHyperDash <= 20.0f)
            {
                if (!catchCurrent.LastObject.HyperDash)
                    edgeDashBonus += 5.7;

                distanceAddition *= 1.0 + edgeDashBonus * ((20 - catchCurrent.LastObject.DistanceToHyperDash) / 20)
                                                        * Math.Pow((Math.Min(catchCurrent.StrainTime * catcherSpeedMultiplier, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
            }

            // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
            // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
            // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
            // To achieve that, we need to store the exact distances (distance ignoring absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH)
            if (current.Index >= 2 && Math.Abs(catchCurrent.ExactDistanceMoved) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                                   && catchCurrent.ExactDistanceMoved == -catchLast.ExactDistanceMoved && catchLast.ExactDistanceMoved == -catchLastLast.ExactDistanceMoved
                                   && catchCurrent.StrainTime == catchLast.StrainTime && catchLast.StrainTime == catchLastLast.StrainTime)
                distanceAddition = 0;

            return distanceAddition / weightedStrainTime;
        }
    }
}
