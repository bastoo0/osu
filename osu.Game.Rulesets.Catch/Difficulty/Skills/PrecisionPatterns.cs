// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    /// <summary>
    /// Peak-oriented aggregation for small-platter precision patterns.
    /// </summary>
    public class PrecisionPatterns : StrainDecaySkill
    {
        protected override double SkillMultiplier => 1;
        protected override double StrainDecayBase => 0.2;
        protected override double DecayWeight => 0.94;
        protected override int SectionLength => 750;

        public PrecisionPatterns(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            return PrecisionPatternsEvaluator.EvaluateDifficultyOf(current);
        }
    }
}
