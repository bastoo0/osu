// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class ControlEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index < 1)
                return 0;

            var catchCurrent = (CatchDifficultyHitObject)current;
            var previous = (CatchDifficultyHitObject)current.Previous(0);

            // A forced hyperdash is already represented by movement strain. Technical control is
            // the need to retime or resize ordinary movement without changing to a forced state.
            if (catchCurrent.LastObject.HyperDash || previous.LastObject.HyperDash)
                return 0;

            double rhythmRatio = Math.Max(catchCurrent.StrainTime, previous.StrainTime)
                                 / Math.Min(catchCurrent.StrainTime, previous.StrainTime);
            double rhythmChange = Math.Min(1, Math.Abs(Math.Log(rhythmRatio, 2)));

            double currentSpeed = Math.Abs(catchCurrent.PhysicalDistanceMoved) / catchCurrent.StrainTime;
            double previousSpeed = Math.Abs(previous.PhysicalDistanceMoved) / previous.StrainTime;
            double maximumSpeed = Math.Max(currentSpeed, previousSpeed);
            double speedChange = maximumSpeed <= 0.001
                ? 0
                : Math.Min(1, Math.Abs(currentSpeed - previousSpeed) / maximumSpeed);

            double controlDemand = Math.Max(rhythmChange, speedChange);

            return MovementEvaluator.EvaluateDifficultyOf(current) * controlDemand;
        }
    }
}
