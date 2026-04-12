// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        private const double direction_change_bonus = 5.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchLast = (CatchDifficultyHitObject)current.Previous(0);
            var catchLastLast = (CatchDifficultyHitObject)current.Previous(1);

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double catcherSpeedMultiplier = current.ClockRate;

            double weightedStrainTime = catchCurrent.StrainTime + 15 + (3 / catcherSpeedMultiplier) + Math.Max(0, 50 - Math.Abs(catchCurrent.DistanceMoved)) * 0.1;

            double distanceAddition = (Math.Abs(catchCurrent.DistanceMoved) / 5000);
            double sqrtStrain = Math.Sqrt(weightedStrainTime);
            double precisionPressure = Math.Max(0.0, catchCurrent.CatcherWidthScale - 1) + 0.2 * Math.Max(0.0, 1 - catchCurrent.CatcherWidthScale);

            double edgeDashBonus = 0;

            // Direction change bonus.
            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1)
            {
                if (current.Index >= 1 && Math.Abs(catchLast.DistanceMoved) > 0.1 && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
                {
                    double bonusFactor = Math.Min(50, Math.Abs(catchCurrent.DistanceMoved)) / 50;
                    double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(catchLast.DistanceMoved)) / 70, 0.38);

                    // Consecutive direction changes are harder: 1.5x when previous was also a direction change
                    double dcMultiplier = 1.0;

                    if (current.Index >= 2 && Math.Sign(catchLast.DistanceMoved) != Math.Sign(catchLastLast.DistanceMoved))
                        dcMultiplier = 1.5;

                    distanceAddition += direction_change_bonus * dcMultiplier / Math.Sqrt(catchLast.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
                }

                // Base bonus for every movement, giving some weight to streams.
                // Sqrt-scaled distance compresses the range: small movements get relatively more credit.
                distanceAddition += 34.0 * Math.Sqrt(Math.Min(Math.Abs(catchCurrent.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2) * CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH)
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
                                    * Math.Max(0.35, 1 - weightedStrainTime / 600);
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
                                        * Math.Max(0.25, 1 - (weightedStrainTime + catchLast.StrainTime) / 1800);
                }
            }

            // Bonus for edge dashes.
            if (catchCurrent.LastObject.DistanceToHyperDash <= 35.0f)
            {
                if (!catchCurrent.LastObject.HyperDash)
                    edgeDashBonus += 5.7;

                distanceAddition *= 1.0 + edgeDashBonus * ((35 - catchCurrent.LastObject.DistanceToHyperDash) / 35)
                                                        * Math.Pow((Math.Min(catchCurrent.StrainTime * catcherSpeedMultiplier, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
            }

            // Cumulative direction changes in last 4 objects
            if (current.Index >= 3)
            {
                var catchPrev3 = (CatchDifficultyHitObject)current.Previous(2);
                int dcCount = 0;

                if (Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved)) dcCount++;
                if (Math.Sign(catchLast.DistanceMoved) != Math.Sign(catchLastLast.DistanceMoved)) dcCount++;
                if (Math.Sign(catchLastLast.DistanceMoved) != Math.Sign(catchPrev3.DistanceMoved)) dcCount++;

                if (dcCount >= 3)
                    distanceAddition += 3.0 / sqrtStrain;
                else if (dcCount >= 2)
                    distanceAddition += 1.5 / sqrtStrain;
            }

            // Rhythm complexity: irregular timing is harder
            if (current.Index >= 1 && catchLast.StrainTime > 1)
            {
                double tRatio = Math.Max(catchCurrent.StrainTime, catchLast.StrainTime)
                              / Math.Min(catchCurrent.StrainTime, catchLast.StrainTime);
                if (tRatio > 1.5)
                    distanceAddition *= 1.0 + 0.15 * Math.Min(tRatio - 1.0, 3.0);
            }

            // Playfield coverage: movement spanning large portion of field is harder
            if (current.Index >= 2)
            {
                double minX = Math.Min(Math.Min(catchCurrent.LastObject.EffectiveX, catchLast.LastObject.EffectiveX), catchLastLast.LastObject.EffectiveX);
                double maxX = Math.Max(Math.Max(catchCurrent.LastObject.EffectiveX, catchLast.LastObject.EffectiveX), catchLastLast.LastObject.EffectiveX);
                double coverage = (maxX - minX) / 512.0;
                if (coverage > 0.5)
                    distanceAddition += 2.0 * (coverage - 0.5) / sqrtStrain;
            }

            // Distance variance: inconsistent movement distances are harder
            if (current.Index >= 2)
            {
                double d0 = Math.Abs(catchCurrent.DistanceMoved);
                double d1 = Math.Abs(catchLast.DistanceMoved);
                double d2 = Math.Abs(catchLastLast.DistanceMoved);
                double mean = (d0 + d1 + d2) / 3;
                double variance = ((d0-mean)*(d0-mean) + (d1-mean)*(d1-mean) + (d2-mean)*(d2-mean)) / 3;
                if (mean > 10)
                    distanceAddition += 0.5 * Math.Sqrt(variance) / (mean * sqrtStrain);
            }

            // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
            // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
            // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
            // To achieve that, we need to store the exact distances (distance ignoring absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH)
            if (current.Index >= 2 && Math.Abs(catchCurrent.ExactDistanceMoved) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                                   && catchCurrent.ExactDistanceMoved == -catchLast.ExactDistanceMoved && catchLast.ExactDistanceMoved == -catchLastLast.ExactDistanceMoved
                                   && catchCurrent.StrainTime == catchLast.StrainTime && catchLast.StrainTime == catchLastLast.StrainTime)
                distanceAddition = 0;

            return distanceAddition * (1.0 + 0.12 * precisionPressure) / Math.Pow(weightedStrainTime, 1.05);
        }
    }
}
