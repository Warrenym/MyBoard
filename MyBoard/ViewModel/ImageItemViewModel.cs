using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class ImageItemViewModel : ObservableObject, IPositionable
    {
        public ImageItem Model { get; }

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private string filePath;

        public ImageItemViewModel(ImageItem model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            filePath = model.FilePath;
        }

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
    }
}
