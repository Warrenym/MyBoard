using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class NoteItemViewModel : ObservableObject, IPositionable, IResizable, ISelectable, ICanvasItemViewModel
    {
        public NoteItem Model { get; }

        // Explicit interface implementation — exposes Model as the general ICanvasItem
        // type without changing the public Model property's specific NoteItem type elsewhere
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
        private string content;

        // Drives the highlight border in the DataTemplate
        [ObservableProperty]
        private bool isSelected;

        // Tracks whether this note is currently in edit mode (TextBox visible)
        [ObservableProperty]
        private bool isEditing;

        public NoteItemViewModel(NoteItem model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            width = model.Width;
            height = model.Height;
            content = model.Content;
        }

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnWidthChanged(double value) => Model.Width = value;
        partial void OnHeightChanged(double value) => Model.Height = value;
        partial void OnContentChanged(string value) => Model.Content = value;
    }
}
