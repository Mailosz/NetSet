using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
using System.Windows.Threading;
using System.Web;
using System.Web.Routing;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for PingBox.xaml
    /// </summary>
    public partial class PingBox : UserControl
    {
        IPAddress[] addresses;
        Ping ping;
        string hostname;
        const double wait = 5000;
        DispatcherTimer timer;
        NetworkInterface netDevice;
        public TimeSpan Interval { get { return timer.Interval; } set { timer.Interval = value; } }
        Queue<double> times = new Queue<double>(32);

        public PingBox(IPAddress ip)
        {
            InitializeComponent();

            addresses = new IPAddress[] { ip };
            ipBlock.Text = ip.ToString();

            ping = new Ping();
            ping.PingCompleted += Ping_PingCompleted;

            timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
            timer.Interval = TimeSpan.FromMilliseconds(5000);
            timer.Tick += Timer_Tick;
            Start();
        }

        public PingBox(string name)
        {
            InitializeComponent();

            hostname = name;
            ipBlock.Text = name;
            GetAddresses();

            ping = new Ping();
            ping.PingCompleted += Ping_PingCompleted;

            timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
            timer.Interval = TimeSpan.FromMilliseconds(5000);
            timer.Tick += Timer_Tick;
            Start();
        }

        private async Task<bool> GetAddresses()
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(hostname);
                if (entry != null)
                {
                    addresses = entry.AddressList;
                    string[] addrNames = new string[addresses.Length];
                    for (int i = 0; i < addresses.Length; i++) addrNames[i] = addresses[i].ToString();
                    ipBlock.Text = entry.HostName + " (" + string.Join(", ", addrNames) + ")";
                    timeBlock.Text = "oczekiwanie";
                    statusIndicator.Fill = new SolidColorBrush(Colors.Silver);
                    statusIndicator.Stroke = new SolidColorBrush(Colors.Gray);
                    return true;
                }
                else throw new Exception();
            }
            catch
            {
                timeBlock.Text = "DNS nie odpowiada";
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                return false;
            }
        }

        //uruchamia funkcję Start()
        private void Timer_Tick(object sender, EventArgs e)
        {
            Start();
        }

        public async void Start()
        {
            timer.Stop();
            if (addresses != null || await GetAddresses())
            {
                try
                {
                    foreach (var addr in addresses)
                    {
                        /*var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sock.Blocking = true;

                        var stopwatch = new Stopwatch();

                        // Measure the Connect call only
                        stopwatch.Start();
                        sock.Connect(addr, 80);
                        stopwatch.Stop();

                        double t = stopwatch.Elapsed.TotalMilliseconds;
                        Console.WriteLine("{0:0.00}ms", t);
                        times.Add(t);

                        sock.Close();*/
                        ping.SendAsync(addr, wait);
                    }
                }
                catch
                {
                    try
                    {
                        ping = new Ping();
                        ping.PingCompleted += Ping_PingCompleted;
                        foreach (var addr in addresses)
                        {
                            ping.SendAsync(addr, wait);
                        }
                    }
                    catch
                    {
                        timeBlock.Text = "nie można pingować";
                        timer.Stop();
                        timer.Start();
                    }
                }
            }//jak nie ma połączenia z adresem DNS
            else timer.Start();
        }

        private void Ping_PingCompleted(object sender, PingCompletedEventArgs e)
        {
            if (e.Reply.Status == IPStatus.Success)
            {
                timeBlock.Text = e.Reply.RoundtripTime.ToString() + " ms";
                statusIndicator.Fill = new SolidColorBrush(Colors.Lime);
                statusIndicator.Stroke = new SolidColorBrush(Colors.Green);

                if (times.Count > 31)
                    times.Dequeue();
                times.Enqueue(e.Reply.RoundtripTime);
            }
            else
            {
                timeBlock.Text = "error";
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
            }
            timer.Start();
            makePath();
        }

        private void makePath()
        {
            if (times.Count > 2)
            {
                PointCollection points = new PointCollection(34);
                points.Add(new Point(0, 0));
                int p = 0;
                foreach (var time in times)
                {
                    points.Add(new Point(p * 4, -time));
                    p++;
                }
                points.Add(new Point((p - 1) * 4, 0));
                polygon.Points = points;
            }
        }


        private void browserItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (addresses != null)
                {
                    foreach (var address in addresses)
                    {
                        Process.Start("http://" + address.ToString() + "/");
                    }
                }
                else
                {

                }
            }
            catch
            {
                MessageBox.Show("Nie można otworzyć tego adresu w przeglądarce.");
            }
        }

        private void portItem_Click(object sender, RoutedEventArgs e)
        {
            if (addresses != null)
            {
                ScanPortWindow window = new ScanPortWindow(addresses);
                window.Show();
            }

        }

        private void deleteItem_Click(object sender, RoutedEventArgs e)
        {
            (Parent as ListBox)?.Items.Remove(this);
        }
    }
}
