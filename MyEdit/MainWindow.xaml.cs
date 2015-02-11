using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace MyEdit
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            

        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".cs";
            dlg.Filter = "CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                (DataContext as ExampleViewModel).NewTab(System.IO.File.ReadAllText(filename));
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    openFolder(dialog.SelectedPath);
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                    break;
                default:
                    break;
            }
        }

        private void openFolder(string p)
        {
            trvMenu.Items.Clear();
            MenuItem root = new MenuItem() { Title = p };
            foreach (var d in Directory.EnumerateDirectories(p).Union(Directory.EnumerateFiles(p)))
            {
                var child = new MenuItem() { Title = d };
                root.Items.Add(child);
            }

            trvMenu.Items.Add(root);
        }

        private void expandFolder(MenuItem root, string p)
        {
            root.Items.Clear();
            foreach (var d in Directory.EnumerateDirectories(p).Union(Directory.EnumerateFiles(p)))
            {
                var child = new MenuItem() { Title = d };
                root.Items.Add(child);
            }
        }

        private void trvMenu_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var menu = e.NewValue as MenuItem;
            if (menu != null)
            {
                var path = menu.Title;
                if (Directory.Exists(path)) expandFolder(menu, path);
                else
                {
                    var doc = System.IO.File.ReadAllText(path);
                    (DataContext as ExampleViewModel).SwitchContent(doc);
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    public class MenuItem
    {
        public MenuItem()
        {
            this.Items = new ObservableCollection<MenuItem>();
        }

        public string Title { get; set; }

        public ObservableCollection<MenuItem> Items { get; set; }
    }
}
