using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyBoard
{
    public partial class MainWindow
    {
        private void NoteEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Visibility == Visibility.Visible)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void NoteEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is NoteItemViewModel note)
                note.IsEditing = false;
        }

        private void NoteEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && sender is TextBox textBox && textBox.DataContext is NoteItemViewModel note)
            {
                note.IsEditing = false;
                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
        }

        private void BoardTitleEditBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not BoardViewModel board) return;
            if (!board.IsEditingTitle) return;

            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BoardTitleEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.Visibility != Visibility.Visible) return;
            if (textBox.DataContext is not BoardViewModel board || !board.IsEditingTitle) return;

            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BoardTitleEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not BoardViewModel board)
                return;

            if (e.Key == Key.Enter)
            {
                board.CommitTitle();
                if (Window.GetWindow(textBox)?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.CurrentBoard.ClearSelection();
                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                board.CommitTitle();
                Keyboard.Focus(RootGrid);
                e.Handled = true;
            }
        }

        private void BoardTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not BoardViewModel board)
                return;

            if (e.ClickCount == 2)
            {
                if (Window.GetWindow(element)?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.CurrentBoard.SelectItem(board);

                board.BeginEditingTitle();
                e.Handled = true;
            }
        }
    }
}