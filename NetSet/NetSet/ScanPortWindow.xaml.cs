using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for ScanPortWindow.xaml
    /// </summary>
    public partial class ScanPortWindow : Window
    {
        IPAddress Address;
        IPAddress[] List;

        ushort start = 0;
        ushort end = 49151;
        Task scanTask;
        bool scanGo;
        public ScanPortWindow(IPAddress address)
        {
            InitializeComponent();
            Address = address;
            ipBlock.Text = Address.ToString();

            /*
            scanTask = new Task(Scan);
            scanTask.GetAwaiter().OnCompleted(scanCompleted);
            scanTask.Start();
            startButton.Content = "Stop"; */
        }

        public ScanPortWindow(IPAddress[] list)
        {
            InitializeComponent();
            List = list;
            if (list.Length > 0)
            {
                Address = list[0];
            }
        }

        private void Scan()
        {
            scanGo = true;
            ushort from = start, to = end;
            Task[] tasks = new Task[to - from + 1];
            ushort i;
            for (i = from; scanGo && i <= to; i++)
            {
                tasks[i - from] = TestPort(i);
            }
            if (!scanGo) tasks = tasks.Take(Math.Max(i-1,0)).ToArray();
            Task.WaitAll(tasks);
        }
        
        private async Task TestPort(ushort port)
        {
            bool success = false;
            
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    var result = client.BeginConnect(Address, port, null, null);
                    success = await Task.Run<bool>(() => result.AsyncWaitHandle.WaitOne(5000));
                }
                catch { }
            }
            Dispatcher.Invoke(() =>
            {
                progressBar.Value+=1;
                if (success)
                    findView.Items.Add(new OpenPort(Address.ToString(), port));
            }, DispatcherPriority.Normal);
        }

        private void scanCompleted()
        {
            startButton.Content = "Skanuj ponownie";
            startButton.IsEnabled = true;
            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;
            fromBox.IsEnabled = true;
            toBox.IsEnabled = true;
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (scanTask == null || scanTask.Status != TaskStatus.Running)
            {
                findView.Items.Clear();
                scanTask = new Task(Scan);
                scanTask.GetAwaiter().OnCompleted(scanCompleted);
                scanTask.Start();

                startButton.Content = "Stop";
                fromBox.IsEnabled = false;
                toBox.IsEnabled = false;
                progressBar.IsIndeterminate = false;
                progressBar.Value = 0;
            }
            else
            {
                scanGo = false;
                startButton.IsEnabled = false;
                fromBox.IsEnabled = true;
                toBox.IsEnabled = true;
                progressBar.IsIndeterminate = true;
            }
                
        }

        private void fromBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ushort.TryParse(fromBox.Text, out start))
            {
                progressBar.Minimum = start;
            }
        }

        private void toBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ushort.TryParse(toBox.Text, out end))
            {
                progressBar.Maximum = end;
            }
        }

        private void fromBox_LostFocus(object sender, RoutedEventArgs e)
        {
            fromBox.Text = start.ToString();
        }

        private void toBox_LostFocus(object sender, RoutedEventArgs e)
        {
            toBox.Text = end.ToString();
        }
    }

    public class OpenPort
    {
        public string Address { get; set; }
        public ushort Port { get; set; }
        public DateTime Time { get; set; }

        public OpenPort(string address, ushort port)
        {
            Address = address;
            Port = port;
            Time = DateTime.Now;
        }
    }
}
