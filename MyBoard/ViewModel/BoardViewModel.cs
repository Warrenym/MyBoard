using CommunityToolkit.Mvvm.ComponentModel;
using MyBoard.Commands;
using MyBoard.Model;
using MyBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace MyBoard.ViewModel
{
    internal partial class BoardViewModel : ObservableObject, IPositionable, ISelectable, ICanvasItemViewModel
    {
        public Board Model { get; }

        ICanvasItem ICanvasItemViewModel.Model => Model;

        [ObservableProperty]
        private double x;

        [ObservableProperty]
        private double y;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string color = "#B39DDB";

        [ObservableProperty]
        private bool isSelected;

        // Tracks which item is currently selected on this board, if any
        [ObservableProperty]
        private object? selectedItem;

        // Tracks whether this board's title is currently being edited (TextBox visible).
        // Not persisted — always starts false, except immediately after creation.
        [ObservableProperty]
        private bool isEditingTitle;

        // The single selected item, if exactly one thing is selected drives which design tools appear in the sidebar
        [ObservableProperty]
        private object? primarySelectedItem;

        public ObservableCollection<object> Items { get; } = new();

        //Undo and Redo
        private readonly UndoRedoManager undoRedo; // shared across the whole board tree


        public BoardViewModel(Board model, UndoRedoManager undoRedo)
        {
            this.undoRedo = undoRedo;
            Model = model; // this line was missing entirely
            x = model.X;
            y = model.Y;
            title = model.Title;
            color = model.Color;


            foreach (var item in model.Items)
                Items.Add(WrapModel(item));
        }


        private object WrapModel(ICanvasItem item) => item switch
        {
            NoteItem note => new NoteItemViewModel(note),
            ImageItem image => new ImageItemViewModel(image),
            Board board => new BoardViewModel(board, undoRedo), // forward the same instance
            _ => throw new NotSupportedException($"Unknown item type: {item.GetType()}")
        };


        partial void OnXChanged(double value) => Model.X = value;
        partial void OnYChanged(double value) => Model.Y = value;
        partial void OnTitleChanged(string value) => Model.Title = value;

        // Creates a new NoteItem, adds it to both the Model and the display collection, and places it at a default position so it doesn't overlap existing items.
        public void AddNote()
        {
            var note = new NoteItem { Content = "New note", X = 100, Y = 100 };
            var vm = new NoteItemViewModel(note);
            undoRedo.Do(new AddItemCommand(Model.Items, Items, note, vm));
        }


        // Creates a new sub-Board, adds it the same way notes are added.
        public BoardViewModel AddBoard()
        {
            var board = new Board { Title = "Unnamed Board", X = 100, Y = 250, ParentBoardId = Model.Id };
            var vm = new BoardViewModel(board, undoRedo);
            undoRedo.Do(new AddItemCommand(Model.Items, Items, board, vm));
            vm.IsEditingTitle = true;
            return vm;

        }


        // Call this instead of directly setting IsEditingTitle = true
        private string titleBeforeEdit = "";
        public void BeginEditingTitle()
        {
            titleBeforeEdit = Title;
            IsEditingTitle = true;
        }


        // Exits title-editing mode. If the user left it blank, defaults to "Unnamed Board"
        public void CommitTitle()
        {
            if (string.IsNullOrWhiteSpace(Title))
                Title = "Unnamed Board";

            if (Title != titleBeforeEdit)
                undoRedo.Record(new RenameCommand(this, titleBeforeEdit, Title));

            IsEditingTitle = false;
        }


        //Changing color of the board
        partial void OnColorChanged(string value) => Model.Color = value;


        // Creates a new ImageItem at the given position — used by drag-and-drop.
        public void AddImage(string filePath, double x, double y)
        {
            // Start at a reasonable base width, with height derived to match the image's real proportions
            const double defaultWidth = 200;
            double aspectRatio = GetImageAspectRatio(filePath);

            var image = new ImageItem
            {
                FilePath = filePath,
                X = x,
                Y = y,
                AspectRatio = aspectRatio,
                Width = defaultWidth,
                Height = defaultWidth / aspectRatio
            };

            var vm = new ImageItemViewModel(image);
            undoRedo.Do(new AddItemCommand(Model.Items, Items, image, vm));
        }


        // Reads an image file's natural pixel dimensions
        private static double GetImageAspectRatio(string filePath)
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                
                bitmap.EndInit();

                // PixelWidth/Height are unaffected by DecodePixelWidth — they reflect the real source size
                return (double)bitmap.PixelWidth / bitmap.PixelHeight;
            }
            catch
            {
                return 1.0; // Fallback to square if the file can't be read for any reason
            }
        }



        // Tracks every currently selected item on this board (supports multi-select).
        // Kept in sync with each item's own IsSelected flag, which drives the highlight border.
        public ObservableCollection<object> SelectedItems { get; } = new();


        // Selects a single item, replacing any existing selection.
        // Used for a normal single click.
        public void SelectItem(object item)
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();

            if (item is ISelectable selectable)
                selectable.IsSelected = true;

            SelectedItems.Add(item);
            PrimarySelectedItem = item;
        }


        // Selects a whole set of items at once, replacing any existing selection.
        // Used by the drag-box multi-select.
        public void SelectItems(IEnumerable<object> items)
        {
            var selection = items.Distinct().ToList();
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();

            foreach (var item in selection)
            {
                if (item is ISelectable selectable)
                    selectable.IsSelected = true;
                SelectedItems.Add(item);
            }

            PrimarySelectedItem = SelectedItems.Count == 1 ? SelectedItems[0] : null;
        }


        // Deselects everything — called when clicking empty canvas space
        public void ClearSelection()
        {
            foreach (var existing in Items.OfType<ISelectable>())
                existing.IsSelected = false;
            SelectedItems.Clear();

            PrimarySelectedItem = null;
            SelectedItem = null;
        }


        // Removes every currently selected item from both the display collection
        // and the underlying Model — now handles multiple items at once
        public void DeleteSelectedItems()
        {
            var removed = SelectedItems
                        .OfType<ICanvasItemViewModel>()
                        .Select(vm => (vm.Model, (object)vm))
                        .ToList();

            ClearSelection();
            if (removed.Count > 0)
                undoRedo.Do(new DeleteItemsCommand(Model.Items, Items, removed));

            SelectedItems.Clear();

            PrimarySelectedItem = null;
        }


        // Copies the current selection into the app clipboard (doesn't remove anything)
        public void CopySelectedItems()
        {
            var models = SelectedItems.OfType<ICanvasItemViewModel>().Select(vm => vm.Model);
            ClipboardService.SetCopy(models);
        }


        // Copies the current selection, then removes it from this board — the "move" half of cut/paste
        public void CutSelectedItems()
        {
            var models = SelectedItems.OfType<ICanvasItemViewModel>().Select(vm => vm.Model).ToList();
            ClipboardService.SetCut(models);
            DeleteSelectedItems();
        }


        // Pastes whatever is in the clipboard at the given canvas position,
        // cascading each subsequent item slightly so a multi-item paste doesn't land as one exact overlapping stack
        public void PasteClipboard(double x, double y)
        {
            if (!ClipboardService.HasContent) return;

            // Anchor point = top-left corner of the clipboard group's bounding box based on the items' ORIGINAL positions (before any paste offset)
            double anchorX = ClipboardService.Items.Min(item => item.X);
            double anchorY = ClipboardService.Items.Min(item => item.Y);

            // One shared delta for the whole group — this is what preserves relative layout, since every item moves by the exact same amount
            double offsetX = (x - anchorX) + 20;
            double offsetY = (y - anchorY) + 20;

            var pasted = new List<object>();

            foreach (var clipboardModel in ClipboardService.Items)
            {
                var clone = CanvasItemClonerService.Clone(clipboardModel);
                if (clone is Board pastedBoard) pastedBoard.ParentBoardId = Model.Id;
                clone.X += offsetX;
                clone.Y += offsetY;

                var vm = WrapModel(clone);
                undoRedo.Do(new AddItemCommand(Model.Items, Items, clone, vm));
                pasted.Add(vm);
            }   

            SelectItems(pasted);

            if (ClipboardService.IsCutOperation)
                ClipboardService.Clear();
        }


        // Duplicates the current selection in place, offset slightly so the copy is visually distinguishable from the original
        public void DuplicateSelectedItems()
        {
            var duplicates = new List<object>();

            foreach (var item in SelectedItems.OfType<ICanvasItemViewModel>().ToList())
            {
                var clone = CanvasItemClonerService.Clone(item.Model);
                if (clone is Board duplicatedBoard) duplicatedBoard.ParentBoardId = Model.Id;
                clone.X += 20;
                clone.Y += 20;

                var vm = WrapModel(clone);
                undoRedo.Do(new AddItemCommand(Model.Items, Items, clone, vm));
                duplicates.Add(vm);
            }

            SelectItems(duplicates);
        }



    }
}
    
