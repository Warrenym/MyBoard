using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Model;

namespace MyBoard.ViewModel
{
    internal partial class NoteItemViewModel : ObservableObject, IPositionable, IResizable, ISelectable, ICanvasItemViewModel
    {
        public NoteItem Model { get; }
        ICanvasItem ICanvasItemViewModel.Model => Model;

        [ObservableProperty] private double x;
        [ObservableProperty] private double y;
        [ObservableProperty] private double width;
        [ObservableProperty] private double height;
        [ObservableProperty] private bool isSelected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        private bool isEditing;

        // The note's actual content — a portable, structured document, not a raw string
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmpty))]
        [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
        private NoteDocument document;

        public NoteItemViewModel(NoteItem model)
        {
            Model = model;
            x = model.X;
            y = model.Y;
            width = model.Width;
            height = model.Height;
            document = model.Document;
        }

        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnWidthChanged(double value) => Model.Width = value;
        partial void OnHeightChanged(double value) => Model.Height = value;
        partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(ShowPlaceholder));

        partial void OnDocumentChanged(NoteDocument value)
        {
            Model.Document = value;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowPlaceholder));
        }

        // True only when the document has exactly one empty block
        public bool IsEmpty =>
            Document.Blocks.Count <= 1 &&
            (Document.Blocks.Count == 0 || Document.Blocks[0].Runs.Count == 0 ||
             Document.Blocks[0].Runs.All(r => string.IsNullOrEmpty(r.Text)));

        private bool HasOnlyDefaultBlock =>
            Document.Blocks.Count <= 1 &&
            (Document.Blocks.Count == 0 || Document.Blocks[0].Type == NoteBlockType.Normal);

        // Controls whether the placeholder should be shown
        public bool ShowPlaceholder => IsEmpty && HasOnlyDefaultBlock && !IsEditing;
    }
}