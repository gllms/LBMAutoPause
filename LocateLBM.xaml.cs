using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

namespace LBMAutoPause
{
    /// <summary>
    /// Interaction logic for LocateLBM.xaml
    /// </summary>
    public partial class LocateLBM : Window
    {
        public LocateLBM()
        {
            InitializeComponent();
            Owner = App.Current.MainWindow;
            textBox.Focus();
        }
        public string LBMPath
        {
            get { return textBox.Text; }
        }

        private void buttonBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                textBox.Text = openFileDialog.FileName;
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
