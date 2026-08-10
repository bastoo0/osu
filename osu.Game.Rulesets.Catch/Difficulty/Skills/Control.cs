// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Control : VariableLengthStrainSkill
    {
        private const double strain_decay_base = 0.2;
        private const double hyperdash_saturation = 1.5;

        private double currentStrain;
        private int objectCount;
        private int controlChangeCount;
        private int hyperDashCount;

        public double ControlChangeShare => objectCount == 0 ? 0 : (double)controlChangeCount / objectCount;

        public Control(Mod[] mods)
            : base(mods, 0.90, 750)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            objectCount++;
            if (catchCurrent.LastObject.HyperDash)
                hyperDashCount++;

            double controlDifficulty = ControlEvaluator.EvaluateDifficultyOf(current);
            if (controlDifficulty > 0)
                controlChangeCount++;

            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += controlDifficulty;
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
            double hyperDashRatio = objectCount == 0 ? 0 : (double)hyperDashCount / objectCount;
            double hyperDashScale = 1 / (1 + hyperdash_saturation * hyperDashRatio);
            return peakDifficulty * hyperDashScale * hyperDashScale;
        }

        private static double strainDecay(double milliseconds) => DiffUtils.Pow(strain_decay_base, milliseconds / 1000);
    }
}
