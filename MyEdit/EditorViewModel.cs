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

// https://github.com/icsharpcode/SharpDevelop/blob/master/src/AddIns/BackendBindings/FSharpBinding/Resources/FS-Mode.xshd
// https://github.com/icsharpcode/SharpDevelop/tree/master/src/Libraries/AvalonEdit/ICSharpCode.AvalonEdit/Highlighting/Resources

namespace MyEdit
{

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

        //private TextDocument document;
        //public TextDocument Document
        //{
        //    get { return document; }
        //    set { this.RaiseAndSetIfChanged(ref document, value); }
        //}

        //private int offset;
        //public int Offset
        //{
        //    get { return offset; }
        //    set { this.RaiseAndSetIfChanged(ref offset, value); }
        //}

        private FrameworkElement tabContent;
        public FrameworkElement TabContent
        {
            get { return tabContent; }
            set { this.RaiseAndSetIfChanged(ref tabContent, value); }
        }

        //private IHighlightingDefinition syntax;
        //public IHighlightingDefinition Syntax
        //{
        //    get { return syntax; }
        //    set { this.RaiseAndSetIfChanged(ref syntax, value); }
        //}

    }


    public sealed class EditorViewModel : ReactiveObject
    {
        public ICommand ExecuteItemCommand { get; set; }
        public ReactiveCommand<TabCommand> OpenFile { get; set; }
        public ReactiveList<string> Items { get; set; }

        private Process process;
        private MyHost myHost;
        private Runspace myRunSpace;
        private PowerShell powershell;
        private App app;
        //private TextEditor editor;
        private IHighlightingDefinition haskellSyntax;


        public EditorViewModel(App app)
        {
            RxApp.MainThreadScheduler = new DispatcherScheduler(Application.Current.Dispatcher);
            this.app = app;

            Items = new ReactiveList<string>();

            var executingAssembly = Assembly.GetExecutingAssembly();
            foreach (var assembly in executingAssembly.GetReferencedAssemblies())
            {
                Items.Add("Referenced assembly: " + assembly.FullName);
            }

            Items.Add(string.Empty);
            Items.Add(string.Empty);
            Items.Add("Type a line and press ENTER, it will be added to the output...");
            Items.Add(string.Empty);


            myHost = new MyHost(app, Items);
            myRunSpace = RunspaceFactory.CreateRunspace(myHost);
            myRunSpace.Open();
            powershell = PowerShell.Create();
            powershell.Runspace = myRunSpace;
            string script = @"ls c:\perso";
            powershell.AddScript(script);
            powershell.AddCommand("out-default");
            powershell.Invoke();


            ExecuteItemCommand = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() => AddItem(o.ToString()));
            });


            TextChanged = ReactiveCommand.CreateAsyncTask(o =>
            {
                return Task.Factory.StartNew(() => {
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
                        var pm = new TabCommand{Title = System.IO.Path.GetFileName(filename), Path = filename, Content= System.IO.File.ReadAllText(filename) };
                        return pm;
                    }
                    else return null;
                });

            }, RxApp.MainThreadScheduler);

            OpenFile.Subscribe(pm => {
                var _doc = new ICSharpCode.AvalonEdit.Document.TextDocument(pm.Content);
                var editor = new TextEditor();
                editor.Document = _doc;
                editor.Document.FileName = pm.Path;
                var trigger = new System.Windows.Interactivity.EventTrigger();
                trigger.EventName = "TextChanged";

                var action = new InvokeCommandAction {  Command = TextChanged };
                trigger.Actions.Add(action);
                trigger.Attach(editor); 
                editor.SyntaxHighlighting = haskellSyntax;
                var tab =new PageModel { TabContent = editor, TabCaption = pm.Title, /*Document = _doc, Syntax = haskellSyntax,*/ IsSelected = true };
                PageModels.Add(tab); 
            });

                        


            using (XmlTextReader reader = new XmlTextReader(@"Syntax\FS-Mode.xshd"))
            {
                haskellSyntax = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            this.PageModels = new ReactiveList<PageModel>();
        }

        private void AddItem(string item)
        {
            powershell.AddScript(item);
            powershell.AddCommand("out-default");
            powershell.Invoke();
        }

        public ReactiveList<PageModel> PageModels { get; set; }
        public ICommand TextChanged { get; set; }

        internal PageModel NewTab(string title, string path, string p)
        {
            var _doc = new ICSharpCode.AvalonEdit.Document.TextDocument(p);
            var editor = new TextEditor();
            //editor.ContextMenu = new ContextMenu();
            editor.Document = _doc;
            editor.Document.FileName = path;
            editor.SyntaxHighlighting = haskellSyntax;
            editor.Document.TextChanged += Document_TextChanged;

            //var trigger = new System.Windows.Interactivity.EventTrigger();
            //trigger.EventName = "TextChanged";

            //var action = new InvokeCommandAction {  Command = TextChanged };
            //trigger.Actions.Add(action);
            //trigger.Attach(editor); 
            
            var pm = new PageModel { TabContent = editor, TabCaption = title, /*Document = _doc, Syntax = haskellSyntax,*/ IsSelected = true};
            //editor.Document.WhenAny(o => o.Text, t => { if (!pm.TabCaption.EndsWith("*")) pm.TabCaption += "*"; });
            //this.PageModels.Add(page);
            return pm;

        }

        void Document_TextChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }



        internal void SwitchContent(string title, string path, string doc)
        {
            NewTab(title, path, doc);
        }

        internal void Save()
        {
            //var page = PageModels.First(pm => pm.IsSelected);
            //(page.TabContent as TextEditor).Save(page.Document.FileName);
            //page.TabCaption = page.TabCaption.Replace("*", "");
        }
    }
}
