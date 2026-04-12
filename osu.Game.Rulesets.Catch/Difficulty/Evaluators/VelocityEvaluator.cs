// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    /// <summary>
    /// Measures raw catcher velocity: how fast the catcher must move between objects.
    /// A fundamentally different signal from the bonus-based MovementEvaluator.
    /// </summary>
    public static class VelocityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            double strainTime = catchCurrent.StrainTime;
            double distance = Math.Abs(catchCurrent.DistanceMoved);

            if (distance < 0.1 || strainTime < 1)
                return 0;

            double velocity = distance / strainTime;

            return Math.Pow(velocity, 1.5);
        }
    }
}
