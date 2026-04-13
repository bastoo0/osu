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
        private const double base_harmonic_scale = 13.0;
        private const double decay_exponent = 0.8;

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

            // Dual adaptive: both harmonic scale and decay exponent adjust based on
            // the number of contributing objects. Longer maps get wider weighting windows
            // AND more even weighting across objects, boosting sustained difficulty.
            double lengthRatio = difficulties.Length / 1000.0;
            double adaptiveScale = base_harmonic_scale * Math.Pow(lengthRatio, 0.15);
            double adaptiveDecay = decay_exponent;

            foreach (double note in difficulties.OrderDescending())
            {
                double weight = (1 + (adaptiveScale / (1 + index)))
                                / (Math.Pow(index, adaptiveDecay) + 1 + (adaptiveScale / (1 + index)));

                difficulty += note * weight;
                index++;
            }

            // Length scaling: longer maps have more sustained difficulty
            double lengthBonus = 1 + 0.05 * Math.Log(Math.Max(difficulties.Length, 1));
            return difficulty * lengthBonus;
        }
    }
}
