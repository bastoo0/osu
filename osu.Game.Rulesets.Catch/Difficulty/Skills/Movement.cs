// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : VariableLengthStrainSkill
    {
        private const double strain_decay_base = 0.2;
        private const double hyperdash_saturation = 0.3;

        private double currentStrain;
        private int objectCount;
        private int hyperDashCount;
        private int dashStateChangeCount;
        private double activeDuration;
        private int fruitCount;

        public double ActiveObjectRate => activeDuration <= 0 ? 0 : objectCount * 1000 / activeDuration;

        public double ActiveDuration => activeDuration;

        public int DifficultyObjectCount => objectCount;

        public double FruitRatio => objectCount == 0 ? 0 : (double)fruitCount / objectCount;

        public double DashStateChangeShare => objectCount <= 1 ? 0 : (double)dashStateChangeCount / (objectCount - 1);

        public double SustainedStrainRatio { get; private set; }

        public Movement(Mod[] mods)
            : base(mods, 0.90, 750)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            objectCount++;
            activeDuration += Math.Min(current.DeltaTime, 1000);
            if (catchCurrent.BaseObject is Fruit)
                fruitCount++;
            if (catchCurrent.LastObject.HyperDash)
                hyperDashCount++;
            if (current.Index >= 1
                && catchCurrent.LastObject.HyperDash != ((CatchDifficultyHitObject)current.Previous(0)).LastObject.HyperDash)
                dashStateChangeCount++;

            currentStrain *= strainDecay(catchCurrent.StrainTime);
            currentStrain += MovementEvaluator.EvaluateDifficultyOf(current);

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
            double maximumStrain = ObjectDifficulties.Count == 0 ? 0 : ObjectDifficulties.Max();
            SustainedStrainRatio = maximumStrain <= 0 ? 0 : sustainedDifficulty / maximumStrain;
            double hyperDashRatio = objectCount == 0 ? 0 : (double)hyperDashCount / objectCount;
            double hyperDashScale = 1 / (1 + hyperdash_saturation * hyperDashRatio);

            return (peakDifficulty + sustainedDifficulty) * hyperDashScale * hyperDashScale;
        }

        public IEnumerable<double> GetCurrentStrainPeakValues() => GetCurrentStrainPeaks().Select(peak => peak.Value);

        private static double strainDecay(double milliseconds) => DiffUtils.Pow(strain_decay_base, milliseconds / 1000);
    }
}
