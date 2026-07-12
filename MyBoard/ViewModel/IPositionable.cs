using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    internal interface IPositionable
    {
        double X { get; set; }
        double Y { get; set; }
    }
}
