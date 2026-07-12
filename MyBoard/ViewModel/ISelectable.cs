using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    // Marks a ViewModel as selectable — the canvas highlights it when IsSelected is true, and Delete removes it when it's the currently selected item.
    internal interface ISelectable
    {
        bool IsSelected { get; set; }
    }
}
