// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
        private const double alternating_stamina_bonus = 0.0212;
        private const int maximum_alternating_stamina_run = 64;
        private const double repeated_control_bonus = 0.185;
        private const double maximum_repeated_control_share = 0.318;
        private const double sustained_strain_bonus = 4.14;
        private const double maximum_sustained_strain = 0.0137;
        private const double peak_edge_dash_bonus = 0.2;
        private const double maximum_peak_edge_dash_share = 0.3;
        private const int peak_transition_count = 20;

        private double currentStrain;
        private int objectCount;
        private int hyperDashCount;
        private int mostCommonControlCount;
        private int currentAlternatingRun;
        private int maximumAlternatingRun;
        private int lastMovementDirection;
        private int hardestTransitionCount;

        private readonly Dictionary<(int Timing, int Travel, int Direction, bool Hyperdash), int> controlTransitionCounts = new();
        private readonly List<double> evaluatorDifficulties = new();
        private readonly (double Strain, bool EdgeDash)[] hardestTransitions = new (double, bool)[peak_transition_count];

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

            var controlTransition = (
                Timing: (int)System.Math.Round(catchCurrent.StrainTime / 25),
                Travel: (int)System.Math.Round(catchCurrent.TravelDistance / 20),
                Direction: catchCurrent.MovementDirection,
                Hyperdash: catchCurrent.LastObject.HyperDash);

            controlTransitionCounts.TryGetValue(controlTransition, out int controlCount);
            controlTransitionCounts[controlTransition] = ++controlCount;
            mostCommonControlCount = System.Math.Max(mostCommonControlCount, controlCount);

            if (catchCurrent.MovementDirection != 0)
            {
                if (lastMovementDirection != 0)
                {
                    if (catchCurrent.MovementDirection != lastMovementDirection)
                    {
                        currentAlternatingRun++;
                        maximumAlternatingRun = System.Math.Max(maximumAlternatingRun, currentAlternatingRun);
                    }
                    else
                        currentAlternatingRun = 0;
                }

                lastMovementDirection = catchCurrent.MovementDirection;
            }

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

            double evaluatorDifficulty = MovementEvaluator.EvaluateDifficultyOf(current);
            evaluatorDifficulties.Add(evaluatorDifficulty);
            currentStrain += evaluatorDifficulty * controlScale;

            recordHardestTransition(currentStrain,
                !catchCurrent.LastObject.HyperDash && catchCurrent.LastObject.DistanceToHyperDash <= 20);

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
            double repeatedControlShare = objectCount == 0 ? 0 : (double)mostCommonControlCount / objectCount;
            double peakEdgeDashShare = hardestTransitionCount == 0
                ? 0
                : (double)hardestTransitions.Take(hardestTransitionCount).Count(transition => transition.EdgeDash) / hardestTransitionCount;

            evaluatorDifficulties.Sort();
            double medianEvaluatorDifficulty = evaluatorDifficulties.Count == 0
                ? 0
                : (evaluatorDifficulties[(evaluatorDifficulties.Count - 1) / 2] + evaluatorDifficulties[evaluatorDifficulties.Count / 2]) / 2;

            // A forced dash is mechanically demanding, but a map made mostly of forced dashes is
            // not proportionally harder for every additional one: the catcher is already in the
            // same held-dash control state. Saturate that repeated demand at map scale while the
            // evaluator continues to price the travel and landing of each individual transition.
            double hyperDashScale = 1 / (1 + hyperdash_saturation * hyperDashRatio);

            // Isolated reversals are handled by the per-object control bonus above. Sustaining a
            // long left-right sequence adds a smaller map-level control-stamina demand. Square-root
            // growth and a fixed cap keep marathon patterns relevant without making length itself
            // an unlimited source of difficulty.
            double alternatingStaminaScale = System.Math.Exp(alternating_stamina_bonus
                                                              * System.Math.Sqrt(System.Math.Min(maximumAlternatingRun, maximum_alternating_stamina_run)));

            // Repeating one timing, travel and input state creates sustained control demand even
            // when no individual transition is exceptional. The median evaluator strain ensures
            // that a map must also sustain meaningful movement, rather than gaining difficulty
            // from repetition alone. Both contributions saturate at map scale.
            double repeatedControlScale = System.Math.Exp(
                repeated_control_bonus * System.Math.Min(repeatedControlShare, maximum_repeated_control_share)
                + sustained_strain_bonus * System.Math.Min(medianEvaluatorDifficulty, maximum_sustained_strain));

            // A near-hyper transition requires deliberately using the edge of the catcher's range.
            // Reward it only when it appears among the map's hardest transitions: raw edge-dash
            // counts also include long, repetitive maps where that precision is not peak demand.
            double peakEdgeDashScale = System.Math.Exp(
                peak_edge_dash_bonus * System.Math.Min(peakEdgeDashShare, maximum_peak_edge_dash_share));

            // Difficulty is square-rooted into star rating, hence the squared scale here.
            return (peakDifficulty + sustainedDifficulty)
                   * hyperDashScale * hyperDashScale
                   * alternatingStaminaScale * alternatingStaminaScale
                   * repeatedControlScale * repeatedControlScale
                   * peakEdgeDashScale * peakEdgeDashScale;
        }

        private void recordHardestTransition(double strain, bool edgeDash)
        {
            int insertionIndex = hardestTransitionCount;

            if (hardestTransitionCount < peak_transition_count)
                hardestTransitionCount++;
            else
            {
                insertionIndex--;

                if (strain <= hardestTransitions[insertionIndex].Strain)
                    return;
            }

            while (insertionIndex > 0 && strain > hardestTransitions[insertionIndex - 1].Strain)
            {
                hardestTransitions[insertionIndex] = hardestTransitions[insertionIndex - 1];
                insertionIndex--;
            }

            hardestTransitions[insertionIndex] = (strain, edgeDash);
        }

        private static double strainDecay(double milliseconds) => DiffUtils.Pow(strain_decay_base, milliseconds / 1000);
    }
}
