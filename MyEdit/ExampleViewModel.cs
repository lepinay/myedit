using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using System.Diagnostics;

namespace MyEdit
{
    public sealed class ExampleViewModel : BaseViewModel
    {
        private ICommand _executeItemCommand;
        private readonly ObservableCollection<string> _items;

        public ExampleViewModel()
        {
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
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = @"/c " + item,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.ASCII

                }
            };

            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                string line = p.StandardOutput.ReadLine();
                _items.Add(line);
                // do something with line
            }
            
        }
    }
}
