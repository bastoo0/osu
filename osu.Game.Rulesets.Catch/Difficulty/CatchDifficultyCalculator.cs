// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 5.54;
        private const double low_ar_reading_bonus = 0.04;
        private const double maximum_reading_bonus = 0.12;
        private const double control_skill_weight = 0.62;

        public override int Version => 20260823;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            Movement movement = skills.OfType<Movement>().Single();
            Control control = skills.OfType<Control>().Single();
            double clockRate = ModUtils.CalculateRateWithMods(mods);
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(
                beatmap.Difficulty.ApproachRate,
                CatchHitObject.PREEMPT_RANGE) / clockRate;
            double effectiveApproachRate = IBeatmapDifficultyInfo.InverseDifficultyRange(
                preempt,
                CatchHitObject.PREEMPT_RANGE);
            double lowArReading = Math.Sqrt(Math.Max(0, 9.25 - effectiveApproachRate));
            double readingScale = Math.Exp(Math.Min(
                maximum_reading_bonus,
                low_ar_reading_bonus * lowArReading));
            double movementDifficulty = movement.DifficultyValue();
            double controlDifficulty = control.DifficultyValue();

            // Timing and spacing changes only become difficult when the underlying movement is
            // meaningful. This prevents low-strain pattern variety from dominating easy maps.
            double controlScale = Math.Min(1, movementDifficulty);
            double combinedDifficulty = movementDifficulty + control_skill_weight * controlScale * controlDifficulty;
            // Keep SR length influence modest because performance already rewards length.
            // Common short maps reach a neutral scale without letting marathon maps dominate.
            double objectCountScale = Math.Clamp(
                Math.Pow(Math.Max(1, movement.DifficultyObjectCount) / 650.0, 0.1),
                0.94,
                1.08);
            double sustainedScale = Math.Exp(0.28 * (movement.SustainedStrainRatio - 0.35));
            // The object-level edge-dash bonus represents precise dash release. Once edge dashes
            // dominate a pattern, that same technique is being repeated rather than introducing
            // independent difficulty at every object. Leave occasional edge dashes untouched.
            double repeatedEdgeDashShare = Math.Clamp((movement.EdgeDashRatio - 0.15) / 0.10, 0, 1);
            double edgeDashScale = 1 - 0.25 * repeatedEdgeDashShare;
            // Droplets can add path and control constraints, so their presence should not reduce
            // movement difficulty. Keep the small bonus for especially fruit-heavy patterns.
            double fruitScale = Math.Exp(0.25 * Math.Max(0, movement.FruitRatio - 0.9));

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = Math.Sqrt(combinedDifficulty) * difficulty_multiplier
                             * readingScale * objectCountScale * sustainedScale * edgeDashScale * fruitScale,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            float halfCatcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;

            // For circle sizes above 5.5, reduce the catcher width further to simulate imperfect gameplay.
            halfCatcherWidth *= 1 - (Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f);

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, halfCatcherWidth, objects, objects.Count));

                lastObject = hitObject;
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new Movement(mods),
                new Control(mods),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new CatchModDoubleTime(),
            new CatchModHalfTime(),
            new CatchModHardRock(),
            new CatchModEasy(),
        };
    }
}
