using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    // Marks a ViewModel as resizable — lets the resize handle work generically across Notes and Images without needing to know which type it's touching.
    internal interface IResizable
    {
        double Width { get; set; }
        double Height { get; set; }
    }
}
