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
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using System.Xml;
using System.Management;
using System.IO;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<NetDevice> netDevices = new List<NetDevice>();
        int index;
        DispatcherTimer statusChecker;
        XmlDocument xml;

        public MainWindow()
        {
            InitializeComponent();
            var searcher = new ManagementObjectSearcher("select * from win32_networkadapter");
            var found = searcher.Get();

            foreach (ManagementObject wmi in found)
            {
                if ((bool)wmi.GetPropertyValue("PhysicalAdapter"))
                {
                    var nd = new NetDevice(wmi);
                    netDevices.Add(nd);

                    TabItem ti = new TabItem();
                    ti.Header = nd.Name;
                    nd.Page = new NetPage(nd);
                    nd.Page.OnAdd += NetPage_OnAdd;
                    nd.Page.OnChange += NetPage_OnChange;
                    nd.Page.OnDelete += NetPage_OnDelete;
                    ti.Content = new Frame() { Content = nd.Page };
                    tabControl.Items.Add(ti);
                }
            }
            
            index = 0;

            xml = new XmlDocument();
            XmlNodeList xmldevices;
            try
            {
                xml.Load("NetworkSettings.xml");
                xmldevices = xml.DocumentElement.GetElementsByTagName("interface");
            }
            catch
            {
                var el = xml.CreateElement("settings");
                xml.AppendChild(el);
                xmldevices = el.ChildNodes;
            }

            foreach (XmlElement element in xmldevices)
            {
                string name = element.GetAttribute("name");
                for (int i = 0; i < netDevices.Count; i++)
                {
                    if (name == netDevices[i].Name)
                    {
                        netDevices[i].Page.SetSettingElement(element);
                        break;
                    }
                }
            }

            statusChecker = new DispatcherTimer(DispatcherPriority.Background);
            statusChecker.Tick += StatusChecker_Tick;
            statusChecker.Interval = TimeSpan.FromSeconds(5);
            statusChecker.Start();
            tabControl.SelectionChanged += TabControl_SelectionChanged;
            StatusCheck();
            DescUpdate();
        }

        private void NetPage_OnAdd(NetworkInterface netDevice, NetworkSetting setting)
        {
            var xmldevices = xml.DocumentElement.GetElementsByTagName("interface");
            foreach (XmlElement element in xmldevices)
            {
                string name = element.GetAttribute("name");
                if (name == netDevice.Name)
                {
                    var nowy = xml.CreateElement("setting");
                    nowy.SetAttribute("name", setting.Name);
                    nowy.SetAttribute("addr", setting.Address);
                    nowy.SetAttribute("mask", setting.Mask);
                    nowy.SetAttribute("gate", setting.Gate);
                    element.AppendChild(nowy);
                    xml.Save("NetworkSettings.xml");
                    return;
                }
            }
            var el = xml.CreateElement("interface");
            el.SetAttribute("name", netDevice.Name);
            xml.DocumentElement.AppendChild(el);
            var neue = xml.CreateElement("setting");
            neue.SetAttribute("name", setting.Name);
            neue.SetAttribute("addr", setting.Address);
            neue.SetAttribute("mask", setting.Mask);
            neue.SetAttribute("gate", setting.Gate);
            el.AppendChild(neue);
            xml.Save("NetworkSettings.xml");

        }

        private void NetPage_OnChange(NetworkInterface netDevice, NetworkSetting before, NetworkSetting data)
        {
            var xmldevices = xml.DocumentElement.GetElementsByTagName("interface");
            foreach (XmlElement element in xmldevices)
            {
                string tagname = element.GetAttribute("name");
                if (tagname == netDevice.Name)
                {
                    var settings = element.GetElementsByTagName("setting");
                    foreach (XmlElement setting in settings)
                    {
                        string name = setting.GetAttribute("name");
                        string addr = setting.GetAttribute("addr");
                        string mask = setting.GetAttribute("mask");
                        string gate = setting.GetAttribute("gate");
                        if (name == before.Name && addr == before.Address && gate == before.Gate && mask == before.Mask && gate == before.Gate)
                        {
                            setting.SetAttribute("name", data.Name);
                            setting.SetAttribute("addr", data.Address);
                            setting.SetAttribute("mask", data.Mask);
                            setting.SetAttribute("gate", data.Gate);
                            break;
                        }
                    }
                }
            }
            xml.Save("NetworkSettings.xml");
        }

        private void NetPage_OnDelete(NetworkInterface netDevice, NetworkSetting data)
        {
            var xmldevices = xml.DocumentElement.GetElementsByTagName("interface");
            foreach (XmlElement element in xmldevices)
            {
                string tagname = element.GetAttribute("name");
                if (tagname == netDevice.Name)
                {
                    var settings = element.GetElementsByTagName("setting");
                    foreach (XmlElement setting in settings)
                    {
                        string name = setting.GetAttribute("name");
                        string addr = setting.GetAttribute("addr");
                        string mask = setting.GetAttribute("mask");
                        string gate = setting.GetAttribute("gate");
                        if (name == data.Name && addr == data.Address && gate == data.Gate && mask == data.Mask && gate == data.Gate)
                        {
                            element.RemoveChild(setting);
                            break;
                        }
                    }
                }
            }
            xml.Save("NetworkSettings.xml");
        }

        private void StatusChecker_Tick(object sender, EventArgs e)
        {
            StatusCheck();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (index != tabControl.SelectedIndex)
            {
                index = tabControl.SelectedIndex;

                DescUpdate();

                statusChecker.Stop();
                statusChecker.Start();

                StatusCheck();
            }
        }

        private void DescUpdate()
        {
            if (index >= 0 && index < netDevices.Count)
            {
                nameLabel.Content = netDevices[index].Desc;

                if (netDevices[index].nic != null)
                {
                    byte[] mac = netDevices[index].nic.GetPhysicalAddress().GetAddressBytes();
                    string[] macstr = new string[mac.Length];
                    for (int i = 0; i < mac.Length; i++)
                    {
                        macstr[i] = mac[i].ToString("X");
                    }
                    macLabel.Content = string.Join("-", macstr);
                }
                else
                {
                    macLabel.Content = "Adres MAC nieznany";
                }
            }
        }

        private async void StatusCheck()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (index >= 0 && index < netDevices.Count)
                {
                    netDevices[index].CheckNIC();
                    if (netDevices[index].nic != null)//enabled
                    {
                        statusIndicator.Fill = new SolidColorBrush(Colors.Lime);
                        statusIndicator.Stroke = new SolidColorBrush(Colors.Green);
                        statusLabel.Content = "On";
                        statusButton.Content = "Wyłącz";
                        statusButton.IsEnabled = true;

                        double received = netDevices[index].nic.GetIPStatistics().BytesReceived;
                        double send = netDevices[index].nic.GetIPStatistics().BytesReceived;
                        double speed = netDevices[index].nic.Speed;

                        statusBlock.Text = "Odebrano: " + ((received > 1073741824) ? ((received / 1073741824).ToString("0.00") + " GB") : ((received > 1048576) ? ((received / 1048576).ToString("0.00") + " MB") : (received / 1024).ToString("0.00") + " KB")) + " \n"
                            + "Wysłano: " + ((send > 1073741824) ? ((send / 1073741824).ToString("0.00") + " GB") : ((send > 1048576) ? ((send / 1048576).ToString("0.00") + " MB") : (send / 1024).ToString("0.00") + " KB")) + " \n"
                            + "Prędkość: " + ((speed > 1073741824) ? ((speed / 1073741824).ToString("0.00") + " GB/s") : ((speed > 1048576) ? ((speed / 1048576).ToString("0.00") + " MB/s") : (speed / 1024).ToString("0.00") + " KB/s"));
                        netDevices[index].Page.ShowProperties();
                    }
                    else
                    {
                        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                        statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                        statusLabel.Content = "Off";
                        statusButton.Content = "Włącz";
                        statusButton.IsEnabled = true;

                        statusBlock.Text = "";
                        netDevices[index].Page.DisabledDevice();
                    }
                }
                else
                {
                    statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                    statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                    statusLabel.Content = "Off";
                    statusButton.Content = "Włącz";
                    statusButton.IsEnabled = false;
                    netDevices[index].Page.DisabledDevice();
                }
            }, DispatcherPriority.Background );

            /*if (index >= 0 && index < netDevices.Count && netDevices[index].nic != null)
            {
                switch (netDevices[index].nic.OperationalStatus)
                {
                    case OperationalStatus.Up:
                        statusIndicator.Fill = new SolidColorBrush(Colors.Lime);
                        statusIndicator.Stroke = new SolidColorBrush(Colors.Green);
                        statusLabel.Content = "On";
                        statusButton.Content = "Wyłącz";
                        statusButton.IsEnabled = true;
                        break;
                    case OperationalStatus.Down:
                        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                        statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
                        statusLabel.Content = "Off";
                        statusButton.Content = "Włącz";
                        statusButton.IsEnabled = true;
                        break;
                    case OperationalStatus.NotPresent:
                        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(192, 192, 192));
                        statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 96, 96));
                        statusLabel.Content = "Brak";
                        //statusButton.Content = "Sprawdź";
                        statusButton.IsEnabled = false;
                        break;
                    case OperationalStatus.Testing:
                        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(64, 128, 255));
                        statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(0, 0, 255));
                        statusLabel.Content = "Test";
                        //statusButton.Content = "Sprawdź";
                        statusButton.IsEnabled = false;
                        break;
                    default:
                        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                        statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(224, 208, 0));
                        statusLabel.Content = "???";
                        statusButton.Content = "Sprawdź";
                        statusButton.IsEnabled = true;
                        break;
                }
            }
            else //something is broken
            {
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(224, 0, 0));
                statusIndicator.Stroke = new SolidColorBrush(Color.FromRgb(96, 0, 0));
            } */
        }

        private async void statusButton_Click(object sender, RoutedEventArgs e)
        {
            if (index >= 0 && index < netDevices.Count)
            {
                netDevices[index].CheckNIC();
                if (netDevices[index].nic != null)//enabled
                {
                    statusChecker.Stop();
                    statusButton.Content = new ProgressBar() { Width = statusButton.ActualWidth - 8, Height = 12, IsIndeterminate = true };
                    statusButton.IsEnabled = false;
                    await Task.Run(()=>netDevices[index].mo.InvokeMethod("Disable", new object[0]));
                    StatusCheck();
                    statusChecker.Start();
                }
                else
                {
                    statusChecker.Stop();
                    statusButton.Content = new ProgressBar() { Width = statusButton.ActualWidth - 8, Height = 12, IsIndeterminate = true };
                    statusButton.IsEnabled = false;
                    await Task.Run(() => netDevices[index].mo.InvokeMethod("Enable", new object[0]));
                    StatusCheck();
                    statusChecker.Start();
                }
            }
        }

        private void moreButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class NetDevice
    {
        public NetworkInterface nic;
        public ManagementObject mo;
        public NetPage Page;
        public string guid;
        public string Desc;
        public string Name;

        public NetDevice(ManagementObject mo)
        {
            this.mo = mo;
            guid = mo.GetPropertyValue("GUID") as string;
            Desc = mo.GetPropertyValue("Name") as string;
            Name = mo.GetPropertyValue("NetConnectionID") as string;
            CheckNIC();
        }

        public void CheckNIC()
        {
            var netDevices = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nd in netDevices)
            {
                if (nd.Id == guid)
                {
                    nic = nd;
                    return;
                }
            }
            nic = null;
        }

    }
}
