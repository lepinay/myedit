using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PowerShell = System.Management.Automation.PowerShell;
using System.Management.Automation.Host;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using System.ComponentModel;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Windows.Data;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using System.IO;
using Ookii.Dialogs.Wpf;

// https://github.com/icsharpcode/SharpDevelop/blob/master/src/AddIns/BackendBindings/FSharpBinding/Resources/FS-Mode.xshd
// https://github.com/icsharpcode/SharpDevelop/tree/master/src/Libraries/AvalonEdit/ICSharpCode.AvalonEdit/Highlighting/Resources

namespace MyEdit
{
    public class MenuItem
    {
        public MenuItem()
        {
            this.Items = new ReactiveList<MenuItem>();
        }

        public string Title { get; set; }

        public ReactiveList<MenuItem> Items { get; set; }
    }


    public class PageModel : ReactiveObject
    {
        private string tabCaption;
        public string TabCaption
        {
            get { return tabCaption; }
            set { this.RaiseAndSetIfChanged(ref tabCaption, value); }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set { this.RaiseAndSetIfChanged(ref isSelected, value); }
        }


        private FrameworkElement tabContent;
        public FrameworkElement TabContent
        {
            get { return tabContent; }
            set { this.RaiseAndSetIfChanged(ref tabContent, value); }
        }


    }


    public sealed class EditorViewModel : ReactiveObject
    {
        public ICommand ExecuteItemCommand { get; set; }
        public ICommand TextChanged { get; set; }
        public ReactiveCommand<TabCommand> OpenFile { get; set; }
        public ReactiveCommand<string> OpenFolder { get; set; }
        public ReactiveCommand<MenuItem> TreeviewSelectedItemChanged { get; set; }
        public ReactiveList<string> Items { get; set; }
        public ReactiveList<MenuItem> Tree { get; set; }
        public ReactiveList<PageModel> PageModels { get; set; }
        private MyHost myHost;
        private Runspace myRunSpace;
        private PowerShell powershell;
        private App app;
        private IHighlightingDefinition haskellSyntax;


        public EditorViewModel(App app)
        {
            RxApp.MainThreadScheduler = new DispatcherScheduler(Application.Current.Dispatcher);
            this.app = app;

            Tree = new ReactiveList<MenuItem>();
            Items = new ReactiveList<string>();

            TreeviewSelectedItemChanged = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() =>
                {
                    return o as MenuItem;
                });
            });

            TreeviewSelectedItemChanged.Subscribe(item =>
            {
                if (Directory.Exists(item.Title)) expandFolder(item);
                else openTab(new TabCommand { Title = System.IO.Path.GetFileName(item.Title), Path = item.Title, Content = System.IO.File.ReadAllText(item.Title) });
            });

            myHost = new MyHost(app, Items);
            myRunSpace = RunspaceFactory.CreateRunspace(myHost);
            myRunSpace.Open();
            powershell = PowerShell.Create();
            powershell.Runspace = myRunSpace;


            ExecuteItemCommand = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() => AddItem(o.ToString()));
            });


            TextChanged = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() =>
                {
                    var page = PageModels.First(p => p.IsSelected);
                    if (!page.TabCaption.EndsWith("*")) page.TabCaption += "*";
                });
            });

            OpenFile = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() =>
                {
                    Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                    dlg.DefaultExt = ".cs";
                    dlg.Filter = "CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
                    Nullable<bool> result = dlg.ShowDialog();
                    if (result == true)
                    {
                        string filename = dlg.FileName;
                        var pm = new TabCommand { Title = System.IO.Path.GetFileName(filename), Path = filename, Content = System.IO.File.ReadAllText(filename) };
                        return pm;
                    }
                    else return null;
                });

            }, RxApp.MainThreadScheduler);

            OpenFolder = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() =>
                {
                    var dialog = new VistaFolderBrowserDialog();
                    var result = dialog.ShowDialog();
                    return dialog.SelectedPath;
                });

            });

            OpenFolder.Subscribe(openFolder);

            OpenFile.Subscribe(openTab);




            using (XmlTextReader reader = new XmlTextReader(@"Syntax\FS-Mode.xshd"))
            {
                haskellSyntax = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            this.PageModels = new ReactiveList<PageModel>();
        }

        private void openTab(TabCommand pm)
        {
            var _doc = new ICSharpCode.AvalonEdit.Document.TextDocument(pm.Content);
            var editor = new TextEditor();
            editor.Document = _doc;
            editor.Document.FileName = pm.Path;
            var trigger = new System.Windows.Interactivity.EventTrigger();
            trigger.EventName = "TextChanged";

            var action = new InvokeCommandAction { Command = TextChanged };
            trigger.Actions.Add(action);
            trigger.Attach(editor);
            editor.SyntaxHighlighting = haskellSyntax;
            var tab = new PageModel { TabContent = editor, TabCaption = pm.Title, /*Document = _doc, Syntax = haskellSyntax,*/ IsSelected = true };
            PageModels.Add(tab);
        }

        private void openFolder(string p)
        {
            Tree.Clear();
            MenuItem root = new MenuItem() { Title = p };
            foreach (var d in Directory.EnumerateDirectories(p).Union(Directory.EnumerateFiles(p)))
            {
                var child = new MenuItem() { Title = d };
                root.Items.Add(child);
            }

            Tree.Add(root);
        }

        private void expandFolder(MenuItem root)
        {
            root.Items.Clear();
            foreach (var d in Directory.EnumerateDirectories(root.Title).Union(Directory.EnumerateFiles(root.Title)))
            {
                var child = new MenuItem() { Title = d };
                root.Items.Add(child);
            }
        }



        private void AddItem(string item)
        {
            powershell.AddScript(item);
            powershell.AddCommand("out-default");
            powershell.Invoke();
        }


        internal void Save()
        {
            //var page = PageModels.First(pm => pm.IsSelected);
            //(page.TabContent as TextEditor).Save(page.Document.FileName);
            //page.TabCaption = page.TabCaption.Replace("*", "");
        }

    }
}
