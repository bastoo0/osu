// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class ControlEvaluator
    {
        private const double rhythm_change_weight = 2.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index < 1)
                return 0;

            var catchCurrent = (CatchDifficultyHitObject)current;
            var previous = (CatchDifficultyHitObject)current.Previous(0);
            double controlDemand = 0;

            double rhythmRatio = Math.Max(catchCurrent.StrainTime, previous.StrainTime)
                                 / Math.Min(catchCurrent.StrainTime, previous.StrainTime);
            if (rhythmRatio > 1.25)
                controlDemand += rhythm_change_weight * Math.Min(1, (rhythmRatio - 1.25) / 0.75);

            return MovementEvaluator.EvaluateDifficultyOf(current) * controlDemand;
        }
    }
}
