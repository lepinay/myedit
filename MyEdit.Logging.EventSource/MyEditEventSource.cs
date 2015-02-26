using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyEdit.Logging.EventSource
{
    [EventSource(Name = "MyEdit2")]
    public sealed class MyEditEventSource : Microsoft.Diagnostics.Tracing.EventSource
    {
        public static MyEditEventSource Log = new MyEditEventSource();

        private string fill(int depth, string p)
        {
            return Enumerable.Range(0, depth).Aggregate(p, (curr, i) => "X" + curr);
        }


        public void NoChange(int depth) { WriteEvent(1, fill(depth, "NoChange")); }
        public void Render(int depth) { WriteEvent(2, fill(depth, "Render")); }
        public void Reuse(int depth) { WriteEvent(3, fill(depth, "Reuse")); }
        public void TabItem(int depth) { WriteEvent(4, fill(depth, "TabItem")); }
        public void LookingForTabItem(int depth) { WriteEvent(5, fill(depth, "LookingForTabItem")); }
        public void TabControl(int depth) { WriteEvent(6, fill(depth, "TabControl")); }
        public void Dock(int depth) { WriteEvent(7, fill(depth, "Dock")); }
        public void Docked(int depth) { WriteEvent(8, fill(depth, "Docked")); }
        public void Column(int depth) { WriteEvent(9, fill(depth, "Column")); }
        public void Row(int depth) { WriteEvent(10, fill(depth, "Row")); }
        public void Editor(int depth) { WriteEvent(11, fill(depth, "Editor")); }
        public void Scroll(int depth) { WriteEvent(12, fill(depth, "Scroll")); }
        public void AppendConsole(int depth) { WriteEvent(13, fill(depth, "AppendConsole")); }
        public void TextBox(int depth) { WriteEvent(14, fill(depth, "TextBox")); }
        public void TreeView(int depth) { WriteEvent(15, fill(depth, "TreeView")); }
        public void TreeItem(int depth) { WriteEvent(16, fill(depth, "TreeItem")); }
        public void ReuseFailure(int depth) { WriteEvent(17, fill(depth, "ReuseFailure")); }
        public void Grid(int depth) { WriteEvent(18, fill(depth, "Grid")); }

    }
}
