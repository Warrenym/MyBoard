using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyBoard.ViewModel
{
    internal abstract partial class CanvasItemViewModel : ObservableObject
    {
        public Guid Id { get; }

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        protected CanvasItemViewModel(Guid id, double x, double y)
        {
            Id = id;
            this.x = x;
            this.y = y;
        }
    }
}
