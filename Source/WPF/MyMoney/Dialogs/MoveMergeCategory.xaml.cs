using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for PickYearDialog.xaml
    /// </summary>
    public partial class MoveMergeCategoryDialog : Window
    {
        public MoveMergeCategoryDialog()
        {
            InitializeComponent();

        }
        public enum DragDropChoice
        {
            Move,
            Merge
        }

        public DragDropChoice Choice;
      
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void OnButtonMove(object sender, RoutedEventArgs e)
        {
            Choice = DragDropChoice.Move;
            this.DialogResult = true;
        }

        private void OnButtonMerge(object sender, RoutedEventArgs e)
        {
            Choice = DragDropChoice.Merge;
            this.DialogResult = true;
        }
    }
}
