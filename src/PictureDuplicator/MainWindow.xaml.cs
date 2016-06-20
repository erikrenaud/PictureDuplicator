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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PictureDuplicator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Worker worker = new Worker();

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = worker.Data;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            worker.Start();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            worker.Stop();
            
            base.OnClosing(e);
        }
    }
}
