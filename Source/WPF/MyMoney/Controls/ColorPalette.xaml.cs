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
using System.Xml;
using Walkabout.Data;
using Walkabout.Interfaces.Views;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for ColorPalette.xaml
    /// </summary>
    public partial class ColorPalette : UserControl, IView
    {
        public ColorPalette()
        {
            InitializeComponent();
        }

        public MyMoney Money { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public string Caption => "ColorPalette";

        public object SelectedRow { get; set; }
        public ViewState ViewState { get; set; }
        public string QuickFilter { get; set; }
        public bool IsQueryPanelDisplayed { get; set; }

        public event EventHandler BeforeViewStateChanged;
        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        public void ActivateView()
        {
        }

        public void Commit()
        {
        }

        public ViewState DeserializeViewState(XmlReader reader)
        {
            return null;
        }

        public void FocusQuickFilter()
        {
        }
    }
}
