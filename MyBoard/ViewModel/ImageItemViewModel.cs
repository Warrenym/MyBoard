using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class ImageItemViewModel : ObservableObject, IPositionable, IResizable, ISelectable, ICanvasItemViewModel
    {
        public ImageItem Model { get; }

        ICanvasItem ICanvasItemViewModel.Model => Model;

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private double width;

        [ObservableProperty]
        private double height;

        [ObservableProperty]
        private string filePath;

        [ObservableProperty]
        private double aspectRatio;

        [ObservableProperty]
        private bool isSelected;

        public ImageItemViewModel(ImageItem model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            width = model.Width;
            height = model.Height;
            filePath = model.FilePath;
            aspectRatio = model.AspectRatio;
        }

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnWidthChanged(double value) => Model.Width = value;
        partial void OnHeightChanged(double value) => Model.Height = value;
    }
}
