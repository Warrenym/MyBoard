using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Model
{
    // Plain data container for the user's custom palette.
    // App-wide (not tied to any single board)
    class ColorPaletteData
    {
        public List<string> SavedColors { get; set; } = new();
        public List<string> RecentlyPicked { get; set; } = new();
    }
}
