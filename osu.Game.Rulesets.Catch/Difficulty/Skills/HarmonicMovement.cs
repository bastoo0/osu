// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    /// <summary>
    /// Uses harmonic weighting (like osu!standard Speed) instead of exponential strain decay.
    /// This paradigm weights the hardest individual objects more heavily than section-based peak decay.
    /// </summary>
    public class HarmonicMovement : Skill
    {
        private const double harmonic_scale = 20.0;
        private const double decay_exponent = 0.9;

        public HarmonicMovement(Mod[] mods)
            : base(mods)
        {
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return MovementEvaluator.EvaluateDifficultyOf(current);
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;
            int index = 0;

            double[] difficulties = ObjectDifficulties.Where(p => p > 0).ToArray();

            foreach (double note in difficulties.OrderDescending())
            {
                double weight = (1 + (harmonic_scale / (1 + index)))
                                / (Math.Pow(index, decay_exponent) + 1 + (harmonic_scale / (1 + index)));

                difficulty += note * weight;
                index++;
            }

            return difficulty;
        }
    }
}
