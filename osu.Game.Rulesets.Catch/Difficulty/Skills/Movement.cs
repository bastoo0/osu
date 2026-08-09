// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : VariableLengthStrainSkill
    {
        private const double strain_decay_base = 0.2;
        private const double hyperdash_saturation = 1.1;
        private const double direction_change_bonus = 1.0;
        private const double rhythm_change_bonus = 2.0;

        private double currentStrain;
        private int objectCount;
        private int hyperDashCount;

        public Movement(Mod[] mods)
            : base(mods, 0.90, 750)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            objectCount++;

            if (catchCurrent.LastObject.HyperDash)
                hyperDashCount++;

            // Keep accumulation consistent with the evaluator's 40 ms density safety cap.
            // Otherwise sub-cap objects retain nearly all prior strain while each still adds a
            // full 40 ms contribution, allowing object spam to stack without the intended bound.
            currentStrain *= strainDecay(catchCurrent.StrainTime);

            double controlScale = 1;

            if (current.Index >= 1)
            {
                var previous = (CatchDifficultyHitObject)current.Previous(0);

                // A direction reversal requires a new movement input rather than continuing the
                // previous motion. A large rhythm change likewise requires the player to retime
                // that input instead of repeating a steady pattern.
                if (catchCurrent.MovementDirection != 0 && previous.MovementDirection != 0 && catchCurrent.MovementDirection != previous.MovementDirection)
                    controlScale += direction_change_bonus;

                if (System.Math.Abs(catchCurrent.StrainTime / previous.StrainTime - 1) > 0.25)
                    controlScale += rhythm_change_bonus;
            }

            currentStrain += MovementEvaluator.EvaluateDifficultyOf(current) * controlScale;

            return currentStrain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
            => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        public override double DifficultyValue()
        {
            double difficulty = 0;
            double weightedTime = 0;

            foreach (StrainPeak strain in GetCurrentStrainPeaks().Where(peak => peak.Value > 0))
            {
                double nextWeightedTime = weightedTime + strain.SectionLength / MaxSectionLength;
                double weight = DiffUtils.Pow(DecayWeight, weightedTime) - DiffUtils.Pow(DecayWeight, nextWeightedTime);

                difficulty += strain.Value * weight;
                weightedTime = nextWeightedTime;
            }

            double peakDifficulty = difficulty / (1 - DecayWeight);
            double sustainedDifficulty = ObjectDifficulties.Count == 0 ? 0 : ObjectDifficulties.Average();
            double hyperDashRatio = objectCount == 0 ? 0 : (double)hyperDashCount / objectCount;

            // A forced dash is mechanically demanding, but a map made mostly of forced dashes is
            // not proportionally harder for every additional one: the catcher is already in the
            // same held-dash control state. Saturate that repeated demand at map scale while the
            // evaluator continues to price the travel and landing of each individual transition.
            double hyperDashScale = 1 / (1 + hyperdash_saturation * hyperDashRatio);

            // Difficulty is square-rooted into star rating, hence the squared scale here.
            return (peakDifficulty + sustainedDifficulty) * hyperDashScale * hyperDashScale;
        }

        private static double strainDecay(double milliseconds) => DiffUtils.Pow(strain_decay_base, milliseconds / 1000);
    }
}
