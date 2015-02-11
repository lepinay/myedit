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

// https://github.com/icsharpcode/SharpDevelop/blob/master/src/AddIns/BackendBindings/FSharpBinding/Resources/FS-Mode.xshd
// https://github.com/icsharpcode/SharpDevelop/tree/master/src/Libraries/AvalonEdit/ICSharpCode.AvalonEdit/Highlighting/Resources

namespace MyEdit
{


    public sealed class ExampleViewModel : BaseViewModel
    {
        private ICommand _executeItemCommand;
        private readonly ObservableCollection<string> _items;
        private Process process;
        private MyHost myHost;
        private Runspace myRunSpace;
        private PowerShell powershell;
        private App app;


        public ExampleViewModel(App app)
        {
            // TODO: Complete member initialization
            this.app = app;




            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            _items = new ObservableCollection<string>();

            var executingAssembly = Assembly.GetExecutingAssembly();
            foreach (var assembly in executingAssembly.GetReferencedAssemblies())
            {
                _items.Add("Referenced assembly: " + assembly.FullName);
            }

            _items.Add(string.Empty);
            _items.Add(string.Empty);
            _items.Add("Type a line and press ENTER, it will be added to the output...");
            _items.Add(string.Empty);


            myHost = new MyHost(app, _items);
            myRunSpace = RunspaceFactory.CreateRunspace(myHost);
            myRunSpace.Open();
            powershell = PowerShell.Create();
            powershell.Runspace = myRunSpace;
            string script = @"ls c:\perso";
            powershell.AddScript(script);
            powershell.AddCommand("out-default");
            powershell.Invoke();


            // Check the flags and see if they were set propertly.


            _executeItemCommand = new RelayCommand<string>(AddItem, x => true);
        }

        public IEnumerable<string> Items
        {
            get { return _items; }
        }

        public ICommand ExecuteItemCommand
        {
            get
            {
                return _executeItemCommand;
            }

            set
            {
                SetPropertyAndNotify(ref _executeItemCommand, value, "ExecuteItemCommand");
            }
        }

        private void AddItem(string item)
        {
            Task.Run(() => {
                powershell.AddScript(item);
                powershell.AddCommand("out-default");
                powershell.Invoke();
            });
            
        }
    }
}
