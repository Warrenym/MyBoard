using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.Model
{
    internal interface ICanvasItem
    {
        Guid Id { get; }
        double X { get; set; }
        double Y { get; set; }
    }
}
