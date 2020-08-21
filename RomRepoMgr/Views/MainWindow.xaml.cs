using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RomRepoMgr.Views
{
    public class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}