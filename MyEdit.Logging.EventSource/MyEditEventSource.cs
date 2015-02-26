using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyEdit.Logging.EventSource
{
    // https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource.aspx

    [EventSource(Name = "MyEdit3")]
    public sealed class MyEditEventSource : Microsoft.Diagnostics.Tracing.EventSource
    {
        public static MyEditEventSource Log = new MyEditEventSource();

        private string fill(int depth, string p)
        {
            return Enumerable.Range(0, depth).Aggregate(p, (curr, i) => " " + curr);
        }


        [Event(1, Message = "{1}")]
        public void NoChange(int depth, string message = "") { WriteEvent(1, depth, fill(depth, "NoChange")); }
        
        [Event(2, Message = "{1}")]
        public void Render(int depth, string message ) { WriteEvent(2, depth, fill(depth, "Render " + message)); }
        
        [Event(3, Message = "{1}")]
        public void Reuse(int depth, string message) { WriteEvent(3, depth, fill(depth, "Reuse " + message )); }
        
        [Event(4, Message = "{1}")]
        public void TabItem(int depth, string message = "") { WriteEvent(4, depth, fill(depth, "TabItem")); }
        
        [Event(5, Message = "{1}")]
        public void LookingForTabItem(int depth, string message = "") { WriteEvent(5, depth, fill(depth, "LookingForTabItem")); }
        
        [Event(6, Message = "{1}")]
        public void TabControl(int depth, string message = "") { WriteEvent(6, depth, fill(depth, "TabControl")); }
        
        [Event(7, Message = "{1}")]
        public void Dock(int depth, string message = "") { WriteEvent(7, depth, fill(depth, "Dock")); }
        
        [Event(8, Message = "{1}")]
        public void Docked(int depth, string message = "") { WriteEvent(8, depth, fill(depth, "Docked")); }
        
        [Event(9, Message = "{1}")]
        public void Column(int depth, string message = "") { WriteEvent(9, depth, fill(depth, "Column")); }
        
        [Event(10, Message = "{1}")]
        public void Row(int depth, string message = "") { WriteEvent(10, depth, fill(depth, "Row")); }
        
        [Event(11, Message = "{1}")]
        public void Editor(int depth, string message = "") { WriteEvent(11, depth, fill(depth, "Editor")); }
        
        [Event(12, Message = "{1}")]
        public void Scroll(int depth, string message = "") { WriteEvent(12, depth, fill(depth, "Scroll")); }
        
        [Event(13, Message = "{1}")]
        public void AppendConsole(int depth, string message = "") { WriteEvent(13, depth, fill(depth, "AppendConsole")); }
        
        [Event(14, Message = "{1}")]
        public void TextBox(int depth, string message = "") { WriteEvent(14, depth, fill(depth, "TextBox")); }
        
        [Event(15, Message = "{1}")]
        public void TreeView(int depth, string message = "") { WriteEvent(15, depth, fill(depth, "TreeView")); }
        
        [Event(16, Message = "{1}")]
        public void TreeItem(int depth, string message = "") { WriteEvent(16, depth, fill(depth, "TreeItem")); }
        
        [Event(17, Message = "{1}")]
        public void ReuseFailure(int depth, string message = "") { WriteEvent(17, depth, fill(depth, "ReuseFailure")); }

        [Event(18, Message = "{1}")]
        public void Grid(int depth, string message = "") { WriteEvent(18, depth, fill(depth, "Grid")); }

        [Event(19, Message = "{1}")]
        public void ElementRemoved(int depth, string message = "") { WriteEvent(19, depth, fill(depth, "ElementRemoved")); }

        [Event(20, Message = "{1}")]
        public void UnknownElement(int depth, string message = "") { WriteEvent(20, depth, fill(depth, "UnknownElement")); }

        [Event(21, Message = "{1}")]
        public void Empty(int depth, string message = "") { WriteEvent(21, depth, fill(depth, "Empty")); }

    }
}
