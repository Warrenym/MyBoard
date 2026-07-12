using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    // Lets generic code (like delete) access the underlying Model, without needing to know the specific ViewModel type.
    internal interface ICanvasItemViewModel
    {
        ICanvasItem Model { get; }
    }
}
