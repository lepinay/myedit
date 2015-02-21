using System;
using System.Collections.Generic;
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

namespace MyEdit.Wpf.Controls
{
    /// <summary>
    /// Interaction logic for TabItem.xaml
    /// </summary>
    public partial class TabItem : UserControl
    {
        public TabItem()
        {
            InitializeComponent();
        }

        // Go figure: fsharp compiler can't see theses, so we re expose them
        public TextBlock TabTitle { get { return this.Title; } }
        public Rectangle TabClose { get { return this.CloseButton; } }
    }
}
