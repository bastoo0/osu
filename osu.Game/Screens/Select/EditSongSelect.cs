﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Game.Beatmaps;

namespace osu.Game.Screens.Select
{
    internal class EditSongSelect : SongSelect
    {
        public EditSongSelect() : base(false) { }
        protected override void OnSelected(WorkingBeatmap beatmap) => Exit();
    }
}
