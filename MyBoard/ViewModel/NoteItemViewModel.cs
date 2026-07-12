using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyBoard.ViewModel
{
    internal partial class NoteItemViewModel : ObservableObject, IPositionable
    {
        public NoteItem Model { get; }

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private string content;

        public NoteItemViewModel(NoteItem model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            content = model.Content;
        }

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnContentChanged(string value) => Model.Content = value;
    }
}
