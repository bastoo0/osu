// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
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

        public int PalpableObjectCount => ObjectDifficulties.Count;

        /// <summary>
        /// Ratio of median to 90th-percentile difficulty. Higher = more sustained difficulty.
        /// </summary>
        public double SustainedRatio { get; private set; }

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

            double lengthRatio = difficulties.Length / 1000.0;
            double adaptiveScale = base_harmonic_scale * Math.Pow(lengthRatio, 0.15);

            foreach (double note in difficulties.OrderDescending())
            {
                double weight = (1 + (adaptiveScale / (1 + index)))
                                / (Math.Pow(index, decay_exponent) + 1 + (adaptiveScale / (1 + index)));

                difficulty += note * weight;
                index++;
            }

            double lengthBonus = 1 + 0.05 * Math.Log(Math.Max(difficulties.Length, 1));

            // Compute sustained ratio: median / p90
            if (difficulties.Length >= 10)
            {
                double[] sorted = difficulties.OrderBy(x => x).ToArray();
                double median = sorted[sorted.Length / 2];
                double p90 = sorted[(int)(sorted.Length * 0.9)];
                SustainedRatio = p90 > 0 ? median / p90 : 0;
            }

            return difficulty * lengthBonus;
        }
    }
}
