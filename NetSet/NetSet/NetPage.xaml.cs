using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
using System.Management;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using System.Xml;
using NetSet;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using DotNetProjects.DhcpServer;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for NetPage.xaml
    /// </summary>
    /// 


    public class ActionCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;
        Action<T> Action;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            Action?.Invoke((T)parameter);
        }

        public ActionCommand(Action<T> action)
        {
            Action = action;
        }
    }

    public class HostAddress
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public DateTime Time { get; set; }
        public HostAddress(string name, string address, DateTime time)
        {
            Name = name;
            Address = address;
            Time = time;
        }
    }

    public partial class NetPage : Page
    {
        public delegate void SettingEvent(NetworkInterface netDevice, NetworkSetting setting);
        public delegate void SettingChangeEvent(NetworkInterface netDevice, NetworkSetting before, NetworkSetting setting);
        public event SettingChangeEvent OnChange;
        public event SettingEvent OnAdd;
        public event SettingEvent OnDelete;
        NetDevice netDevice;
        DHCPServer dhcp;
        Task netSearch;
        bool searchGo;
        FontFamily monoFont = new FontFamily("Square721 Cn BT");

        ActionCommand<ListViewItem> ChangeCommand, DeleteCommand, PingGateCommand, GateOpenCommand;
        ActionCommand<IPAddress> PingCommand, ScanPortCommand;

        delegate byte[] ByteArrayAction();

        public NetPage(NetDevice netDevice)
        {
            InitializeComponent();

            this.netDevice = netDevice;

            ShowProperties();

            ChangeCommand = new ActionCommand<ListViewItem>(ChangeSetting);
            DeleteCommand = new ActionCommand<ListViewItem>(DeleteSetting);
            PingCommand = new ActionCommand<IPAddress>(PingAddress);
            PingGateCommand = new ActionCommand<ListViewItem>(PingGate);
            GateOpenCommand = new ActionCommand<ListViewItem>(OpenGate);
            ScanPortCommand = new ActionCommand<IPAddress>(ScanPort);
        }
        public void DisabledDevice()
        {
            leftGrid.IsEnabled = false;
            rightGrid.IsEnabled = false;
            noNetDeviceAlert.Visibility = Visibility.Visible;

            searchGo = false;
            if (netSearch != null && netSearch.Status == TaskStatus.Running) checkButton.IsEnabled = false;
        }

        public void ShowProperties()
        {
            netDevice.CheckNIC();
            if (netDevice.nic != null)
            {
                leftGrid.IsEnabled = true;
                rightGrid.IsEnabled = true;
                noNetDeviceAlert.Visibility = Visibility.Hidden;
                List<UnicastIPAddressInformation> ipv4List = new List<UnicastIPAddressInformation>(ipv4Panel.Children.Count + 1);
                List<UnicastIPAddressInformation> ipv6List = new List<UnicastIPAddressInformation>(ipv6Panel.Children.Count + 1);
                
                foreach (var addr in netDevice.nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        ipv4List.Add(addr);
                    else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        ipv6List.Add(addr);
                }
                ipv4Panel.Children.Clear();
                if (ipv4List.Count == 1)
                {
                    var addr = ipv4List.First();
                    var tb = new TextBlock() { Padding = new Thickness(5), FontSize = 14, FontFamily = monoFont };
                    tb.Text = addr.Address.ToString();
                    if (netDevice.nic.GetIPProperties().GetIPv4Properties().IsDhcpEnabled) tb.Text += " (DHCP)";
                    ipv4Panel.Children.Add(tb);
                    maskBlock.Text = addr.IPv4Mask.ToString();
                    ipv4TextBlock.Text = "Adres";
                    ipv4MaskGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    foreach (var addr in ipv4List)
                    {
                        var tb = new TextBlock() { Padding = new Thickness(5), FontSize = 14, FontFamily = monoFont };
                        tb.Text = addr.Address.ToString() + " / " + BigInteger.Negate(new BigInteger(addr.IPv4Mask.GetAddressBytes().Reverse().ToArray())).ToString();
                        tb.ToolTip = "Mask: " + addr.IPv4Mask.ToString();
                        ipv4Panel.Children.Add(tb);
                    }
                    maskBlock.Text = "";
                    ipv4TextBlock.Text = "Adresy";
                    ipv4MaskGrid.Visibility = Visibility.Collapsed;
                }

                ipv6Panel.Children.Clear();
                foreach (var addr in ipv6List)
                {
                    var tb = new TextBlock() { Padding = new Thickness(5), FontSize = 14, FontFamily = monoFont };
                    tb.Text = addr.Address.ToString();
                    if (netDevice.nic.GetIPProperties().GetIPv4Properties().IsDhcpEnabled) tb.Text += " (DHCP)";
                    ipv6Panel.Children.Add(tb);
                }

                gateStack.Children.Clear();
                ipv6GatesPanel.Children.Clear();
                foreach (var addr in netDevice.nic.GetIPProperties().GatewayAddresses)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = addr.Address.ToString();
                    tb.FontSize = 14;
                    tb.FontFamily = monoFont;
                    tb.Padding = new Thickness(5);

                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        gateStack.Children.Add(tb);
                    else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        ipv6GatesPanel.Children.Add(tb);
                }

                dnsWrap.Children.Clear();
                foreach (var addr in netDevice.nic.GetIPProperties().DnsAddresses)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = addr.ToString();
                    tb.FontSize = 14;
                    tb.FontFamily = monoFont;
                    tb.Padding = new Thickness(5);

                    dnsWrap.Children.Add(tb);
                }
            }
            else DisabledDevice();
        }

        public void SetSettingElement(XmlElement element)
        {
            var list = element.GetElementsByTagName("setting");
            foreach (XmlElement setting in list)
            {
                string name = setting.GetAttribute("name");
                string address = setting.GetAttribute("addr");
                string mask = setting.GetAttribute("mask");
                string gate = setting.GetAttribute("gate");

                ListViewItem lvi = new ListViewItem();
                lvi.Content = new NetworkSetting(name, address, mask, gate);
                lvi.MouseDoubleClick += Setting_MouseDoubleClick;
                var cm = new ContextMenu();
                cm.Items.Add(new MenuItem() { Header = "Otwórz bramę", Command = GateOpenCommand, CommandParameter = lvi });
                cm.Items.Add(new MenuItem() { Header = "Pinguj bramę", Command = PingGateCommand, CommandParameter = lvi });
                cm.Items.Add(new MenuItem() { Header="Zmień", Command = ChangeCommand, CommandParameter = lvi });
                cm.Items.Add(new MenuItem() { Header="Usuń", Command = DeleteCommand, CommandParameter = lvi });
                if (gate == "") { ((MenuItem)cm.Items[0]).IsEnabled = false; ((MenuItem)cm.Items[1]).IsEnabled = false; }
                lvi.ContextMenu = cm;
                listView.Items.Add(lvi);
            }
        }

        private void Setting_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var setting = (sender as ListViewItem)?.Content as NetworkSetting;
            if (setting != null)
            {
                setStaticIP(setting.Address, setting.Mask, setting.Gate);
                ShowProperties();
            }
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            SetWindow window = new SetWindow();
            window.OnSubmit += AddWindow_OnSubmit;
            window.Show();
        }

        private void AddWindow_OnSubmit(NetworkSetting before, NetworkSetting setting)
        {
            setStaticIP(setting.Address, setting.Mask, setting.Gate);
            OnAdd?.Invoke(netDevice.nic, setting);
            ListViewItem lvi = new ListViewItem();
            lvi.Content = setting;
            lvi.MouseDoubleClick += Setting_MouseDoubleClick;
            var cm = new ContextMenu();
            cm.Items.Add(new MenuItem() { Header = "Otwórz bramę", Command = GateOpenCommand, CommandParameter = lvi });
            cm.Items.Add(new MenuItem() { Header = "Pinguj bramę", Command = PingGateCommand, CommandParameter = lvi });
            cm.Items.Add(new MenuItem() { Header = "Zmień", Command = ChangeCommand, CommandParameter = lvi });
            cm.Items.Add(new MenuItem() { Header = "Usuń", Command = DeleteCommand, CommandParameter = lvi });
            if (setting.Gate == "") { ((MenuItem)cm.Items[0]).IsEnabled = false; ((MenuItem)cm.Items[1]).IsEnabled = false; }
            lvi.ContextMenu = cm;
            listView.Items.Add(lvi);
            ShowProperties();
        }

        private void ChangeSetting(ListViewItem item)
        {
            SetWindow window = new SetWindow(item.Content as NetworkSetting);
            window.OnSubmit += ChangeWindow_OnSubmit; ;
            window.Show();
        }

        private void ChangeWindow_OnSubmit(NetworkSetting before, NetworkSetting data)
        {
            setStaticIP(data.Address, data.Mask, data.Gate);
            OnChange?.Invoke(netDevice.nic, before, data);
            foreach (ListViewItem lvi in listView.Items)
            {
                if (lvi.Content == before)
                {
                    if (data.Gate == "") { ((MenuItem)lvi.ContextMenu.Items[0]).IsEnabled = false; ((MenuItem)lvi.ContextMenu.Items[1]).IsEnabled = false; }
                    else { ((MenuItem)lvi.ContextMenu.Items[0]).IsEnabled = true; ((MenuItem)lvi.ContextMenu.Items[1]).IsEnabled = true; }
                    lvi.Content = data;
                    break;
                }
            }
        }

        private void DeleteSetting(ListViewItem item)
        {
            OnDelete?.Invoke(netDevice.nic, item.Content as NetworkSetting);
            listView.Items.Remove(item);
        }

        private async void autoIPButton_Click(object sender, RoutedEventArgs e)
        {
            App.RunNetsh("interface ipv4 set address name=\"" + netDevice.Name + "\" dhcp");
            ipv4Panel.Children.Clear();
            //ipv4Panel.Children.Re.Text = "oczekiwanie na DHCP";
            await Task.Delay(5000);
            ShowProperties();
        }

        private void setStaticIP(string _ip, string _mask, string _gate)
        {
            string arg = "interface ipv4 set address name=\"" + netDevice.Name + "\" static " + _ip + " " + _mask + " " + _gate;
            App.RunNetsh(arg);
        }

        private void pingButton_Click(object sender, RoutedEventArgs e)
        {
            insertPingItem();
        }

        private void addPingBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                insertPingItem();
            }
        }

        private void insertPingItem()
        {
            IPAddress pingip;
            if (IPAddress.TryParse(addPingBox.Text, out pingip))
            {
                addPingBox.Text = "";
                pingList.Items.Insert(pingList.Items.Count - 1, new PingBox(pingip));
            }
            else
            {
                pingList.Items.Insert(pingList.Items.Count - 1, new PingBox(addPingBox.Text));
                addPingBox.Text = "";
            }
        }

        /// <summary>
        /// Przeszukaj aktualną podsieć
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkButton_Click(object sender, RoutedEventArgs e)
        {
            if (netDevice.nic != null)
            {
                if (netSearch == null || netSearch.Status != TaskStatus.Running)
                {
                    searchGo = true;
                    foreach (var addr in netDevice.nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            siteView.Items.Clear();
                            netSearch = new Task(Search, new Tuple<IPAddress, IPAddress>(addr.Address, addr.IPv4Mask));
                            netSearch.GetAwaiter().OnCompleted(netSearchEnded);
                            checkTextBlock.Text = "Zatrzymaj";
                            checkProgress.Visibility = Visibility.Visible;
                            netSearch.Start();
                            break;
                        }
                    }

                }
                else
                {
                    searchGo = false;
                    checkButton.IsEnabled = false;
                    checkTextBlock.Text = "Kończenie";
                }
            }
        }

        private void Search(object site)
        {
            var tuple = (site as Tuple<IPAddress, IPAddress>);
            if (tuple != null)
            {
                var addr = tuple.Item1.GetAddressBytes();
                var mask = tuple.Item2.GetAddressBytes();
                var range = mask.Clone() as byte[];

                for (int i = 0; i < addr.Length; i++)
                {
                    addr[i] &= mask[i];
                    addr[i] |= (byte)(~mask[i]);
                }

                int counter = 0;
                Task[] tasks = new Task[100];
                int num = 0;
                //początkowe przechodzenie przez adres
                while (range[num] == 255) { num++; if (num >= range.Length) return; }
                ByteArrayAction next = () =>
                {
                    for (int i = range.Length - 1; i >= num; i--)
                    {
                        if (range[i] == 255)
                            if (i > num) range[i] = 0;
                            else { num++; break; }
                        else { range[i]++; break; }
                    }
                    var tocheck = new byte[addr.Length];
                    for (int i = 0; i < addr.Length; i++) tocheck[i] = (byte)(addr[i] & range[i]);
                    return tocheck;
                };
                //Dispatcher.Invoke(()=>textBlock.Text = new IPAddress(tocheck).ToString());
                Action<byte[]> step = async (tocheck) =>
                {
                    try
                    {
                        using (Ping ping = new Ping())
                        {
                            PingReply reply = await ping.SendPingAsync(new IPAddress(tocheck), 5000);

                            if (reply.Status == IPStatus.Success)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    textBlock.Text = new IPAddress(tocheck).ToString();
                                    siteView.Items.Add(new ListViewItem() { Content = new HostAddress("znaleziono", reply.Address.ToString(), DateTime.Now) });
                                }, System.Windows.Threading.DispatcherPriority.Normal);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                };
                
                while (searchGo && counter < tasks.Length)
                {
                    if (num > range.Length) return;
                    tasks[counter] = Task.Run(()=>step(next()));
                    counter++;
                }
                while (searchGo && num < range.Length)
                {
                    tasks[Task.WaitAny(tasks)] = Task.Run(() => step(next()));
                }
                if (tasks.Length > counter)
                tasks = tasks.Take(counter).ToArray();
                Task.WaitAll(tasks);
            }
        }

        private void netSearchEnded()
        {
            checkButton.IsEnabled = true;
            checkTextBlock.Text = "Szukaj";
            checkProgress.Visibility = Visibility.Collapsed;
        }

        private void siteView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (siteView.SelectedIndex >= 0)
            {
                var item = siteView.Items[siteView.SelectedIndex] as ListViewItem;
                if (item != null)
                {
                    if (!item.IsMouseOver) e.Handled = true;
                }
            }
        }

        private void dhcpButton_Click(object sender, RoutedEventArgs e)
        {
            if (dhcp == null)
            {
                dhcp = new DHCPServer();
                dhcp.Start();

                dhcp.OnDataReceived += Dhcp_OnDataReceived;

                dhcpButton.Background = new SolidColorBrush(Color.FromRgb(128, 255, 128));
                dhcpStatus.Fill = new SolidColorBrush(Colors.Lime);
                dhcpStatus.Stroke = new SolidColorBrush(Colors.Green);
                dhcpOnOff.Text = "On";
            }
            else
            {
                dhcp.Dispose();

                dhcpButton.Background = new SolidColorBrush(Color.FromRgb(221, 221, 221));
                dhcpStatus.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                dhcpStatus.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                dhcpOnOff.Text = "Off";
            }
            

            
            
            /* old code
            if (dhcp.IsRunning)
            {
                dhcp.Shutdown();
                dhcpButton.Background = new SolidColorBrush(Color.FromRgb(221, 221, 221));
                dhcpStatus.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                dhcpStatus.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                dhcpOnOff.Text = "Off";
            }
            else
            {
                IPAddress address = null;
                foreach (var addr in netDevice.nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = addr.Address;
                        break;
                    }
                }
                if (address != null)
                {
                    if (dhcp.Establish(address))
                    {
                        dhcpButton.Background = new SolidColorBrush(Color.FromRgb(128, 255, 128));
                        dhcpStatus.Fill = new SolidColorBrush(Colors.Lime);
                        dhcpStatus.Stroke = new SolidColorBrush(Colors.Green);
                        dhcpOnOff.Text = "On";
                    }
                }
            }*/
        }

        private void Dhcp_OnDataReceived(DHCPRequest dhcpRequest)
        {
            
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            ShowProperties();
        }

        private void openInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (siteView.SelectedIndex >= 0)
            {
                var addr = (siteView.Items[siteView.SelectedIndex] as ListViewItem)?.Content as HostAddress;
                if (addr != null)
                {
                    System.Diagnostics.Process.Start("http://" + addr.Address + "/");
                }
            }
        }

        private void pingAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (siteView.SelectedIndex >= 0)
            {
                var addr = (siteView.Items[siteView.SelectedIndex] as ListViewItem)?.Content as HostAddress;
                if (addr != null)
                {
                    IPAddress address;
                    if (IPAddress.TryParse(addr.Address, out address))
                        pingList.Items.Insert(pingList.Items.Count - 1, new PingBox(address)); ;
                }
            }
        }

        private void scanPortButtonButton_Click(object sender, RoutedEventArgs e)
        {
            if (siteView.SelectedIndex >= 0)
            {
                var addr = (siteView.Items[siteView.SelectedIndex] as ListViewItem)?.Content as HostAddress;
                if (addr != null)
                {
                    IPAddress address;
                    if (IPAddress.TryParse(addr.Address, out address))
                    {
                        ScanPortWindow window = new ScanPortWindow(address);
                        window.Show();
                    }
                }
            }
        }

        private void retryButton_Click(object sender, RoutedEventArgs e)
        {
            ShowProperties();
        }

        private void propertiesLink_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("NCPA.cpl");
            startInfo.UseShellExecute = true;

            Process.Start(startInfo);
        }

        private void PingAddress(IPAddress addr)
        {
            pingList.Items.Insert(pingList.Items.Count - 1, new PingBox(addr));
        }

        private void PingGate(ListViewItem lvi)
        {
            IPAddress addr;
            if (IPAddress.TryParse((lvi.Content as NetworkSetting)?.Gate, out addr))
                pingList.Items.Insert(pingList.Items.Count - 1, new PingBox(addr));
        }

        private void OpenGate(ListViewItem lvi)
        {
            var addr = lvi.Content as NetworkSetting;
            if (addr != null)
            {
                System.Diagnostics.Process.Start("http://" + addr.Gate + "/");
            }
        }

        private void ScanPort(IPAddress addr)
        {

        }
    }
}
