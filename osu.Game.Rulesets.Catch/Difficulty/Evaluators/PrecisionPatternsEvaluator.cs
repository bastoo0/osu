// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    /// <summary>
    /// Measures peak reading and precision pressure caused by small platters on demanding patterns.
    /// This intentionally excludes broad movement tax and focuses on localized pattern spikes.
    /// </summary>
    public static class PrecisionPatternsEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            double precisionPressure = Math.Max(0.0, catchCurrent.CatcherWidthScale - 1);

            if (precisionPressure <= 0 || current.Index < 2)
                return 0;

            var catchLast = (CatchDifficultyHitObject)current.Previous(0);
            var catchLastLast = (CatchDifficultyHitObject)current.Previous(1);

            double weightedStrainTime = catchCurrent.StrainTime + 15 + (3 / current.ClockRate);
            double sqrtStrain = Math.Sqrt(weightedStrainTime);
            double precisionAddition = 0;

            double edgeProximity = Math.Max(0.0, Math.Abs(catchCurrent.LastObject.EffectiveX - 256) / 256.0 - 0.55) / 0.45;
            double minX = Math.Min(Math.Min(catchCurrent.LastObject.EffectiveX, catchLast.LastObject.EffectiveX), catchLastLast.LastObject.EffectiveX);
            double maxX = Math.Max(Math.Max(catchCurrent.LastObject.EffectiveX, catchLast.LastObject.EffectiveX), catchLastLast.LastObject.EffectiveX);
            double coverage = (maxX - minX) / 512.0;

            if (edgeProximity > 0 && catchCurrent.StrainTime < 220 && Math.Abs(catchCurrent.DistanceMoved) > CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 0.6)
                precisionAddition += 0.6 * precisionPressure * edgeProximity / sqrtStrain;

            if (coverage > 0.55 && catchCurrent.StrainTime < 260)
                precisionAddition += 1.4 * precisionPressure * (coverage - 0.55) / sqrtStrain;

            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1
                && Math.Abs(catchLast.DistanceMoved) > 0.1
                && Math.Abs(catchLastLast.DistanceMoved) > 0.1
                && Math.Sign(catchCurrent.DistanceMoved) == Math.Sign(catchLastLast.DistanceMoved)
                && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
            {
                double reversalSize = Math.Min(Math.Abs(catchCurrent.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                      / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2);
                precisionAddition += 1.0 * precisionPressure * reversalSize / Math.Sqrt(catchLastLast.StrainTime + 20);
            }

            if (catchLast.StrainTime > 1)
            {
                double timeRatio = Math.Max(catchCurrent.StrainTime, catchLast.StrainTime)
                                   / Math.Min(catchCurrent.StrainTime, catchLast.StrainTime);

                if (timeRatio > 1.5)
                    precisionAddition *= 1.0 + 0.25 * Math.Min(timeRatio - 1.0, 2.5);
            }

            return precisionAddition;
        }
    }
}
