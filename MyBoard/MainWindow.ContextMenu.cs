using MyBoard.ViewModel;
using System.Windows;

namespace MyBoard
{
    public partial class MainWindow
    {
        private void MenuItem_Cut_Click(object sender, RoutedEventArgs e) =>
            ((MainViewModel)DataContext).CurrentBoard.CutSelectedItems();

        private void MenuItem_Copy_Click(object sender, RoutedEventArgs e) =>
            ((MainViewModel)DataContext).CurrentBoard.CopySelectedItems();

        private void MenuItem_Duplicate_Click(object sender, RoutedEventArgs e) =>
            ((MainViewModel)DataContext).CurrentBoard.DuplicateSelectedItems();

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e) =>
            ((MainViewModel)DataContext).CurrentBoard.DeleteSelectedItems();

        private void MenuItem_Rename_Click(object sender, RoutedEventArgs e)
        {
            var board = ((MainViewModel)DataContext).CurrentBoard;
            if (board.SelectedItems.Count == 1 && board.SelectedItems[0] is BoardViewModel boardToRename)
                boardToRename.BeginEditingTitle();
        }

        private void MenuItem_PasteCanvas_Click(object sender, RoutedEventArgs e)
        {
            var board = ((MainViewModel)DataContext).CurrentBoard;
            board.PasteClipboard(lastCanvasMousePosition.X, lastCanvasMousePosition.Y);
        }
    }
}