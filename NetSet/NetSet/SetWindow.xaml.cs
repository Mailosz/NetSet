using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for SetWindow.xaml
    /// </summary>
    /// 

    public class NetworkSetting
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Mask { get; set; }
        public string Gate { get; set; }
        public string Dns { get; set; }

        public NetworkSetting(string name, string addr, string mask, string gate)
        {
            Name = name;
            Address = addr;
            Mask = mask;
            Gate = gate;
        }

        public NetworkSetting(string name, string addr, string mask, string gate, string dns) : this(name, addr, mask, gate)
        {
            Dns = dns;
        }
    }
    
    public partial class SetWindow : Window
    {
        public IPAddress Address;
        public IPAddress Mask;
        public IPAddress Gate;

        NetworkSetting Before = null;
        Brush standardBorderBrush;

        public delegate void WindowSubmitEvent(NetworkSetting before, NetworkSetting data);
        public event WindowSubmitEvent OnSubmit;

        public SetWindow()
        {
            InitializeComponent();
            standardBorderBrush = ipBox.BorderBrush;
        }

        public SetWindow(NetworkSetting before)
        {
            InitializeComponent();
            Before = before;

            nameBox.Text = before.Name;
            ipBox.Text = before.Address;
            maskBox.Text = before.Mask;
            gateBox.Text = before.Gate;
        }



        private void submitButton_Click(object sender, RoutedEventArgs e)
        { 
            if (IPAddress.TryParse(ipBox.Text, out Address))
            {
                if (IPAddress.TryParse(maskBox.Text, out Mask))
                {
                    if (gateBox.Text == "" || IPAddress.TryParse(gateBox.Text, out Gate))
                    {
                        if (dnsCheckbox.IsChecked == false)
                        {
                            OnSubmit?.Invoke(Before, new NetworkSetting(nameBox.Text, Address.ToString(), Mask.ToString(), Gate?.ToString() ?? ""));
                            this.Close();
                        }
                        else if (multiBoxCheck(dnsBox))
                        {
                            OnSubmit?.Invoke(Before, new NetworkSetting(nameBox.Text, Address.ToString(), Mask.ToString(), Gate?.ToString() ?? "", dnsBox.Text));
                            this.Close();
                        }
                        dnsBox.Focus();                                                         
                    }
                    else
                    {
                        gateBox.Focus();
                    }
                }
                else
                {
                    maskBox.Focus();
                }
            }
            else
            {
                ipBox.Focus();
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ipv4Box_LostFocus(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(ipBox.Text, out Mask))
            {
                ipBox.BorderBrush = standardBorderBrush;
            }
            else ipBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        }

        private void maskBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(maskBox.Text, out Mask))
            {
                maskBox.BorderBrush = standardBorderBrush;
            }
            else maskBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        }

        private void gateBox_LostFocus(object sender, RoutedEventArgs e)
        {
            multiBoxCheck(sender as TextBox);
        }

        private bool multiBoxCheck(TextBox tb)
        {
            if (tb != null)
            {
                string[] lines = tb.Text.Split(';', '\n');
                IPAddress addr;
                foreach (var line in lines)
                {
                    string t = line.Trim(' ');
                    if (t.Length > 0 && !IPAddress.TryParse(t, out addr))
                    {
                        tb.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        return false;
                    }
                }
                tb.BorderBrush = standardBorderBrush;
                return true;
            }
            return false;
        }

        private void multiBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null)
            {
                string[] lines = tb.Text.Split(';', '\n');
                tb.MaxLines = lines.Length;
                int caret = tb.CaretIndex;
                tb.Text = string.Join("\n", lines);
                tb.CaretIndex = caret;
            }
        }

        private void dnsBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dnsCheckbox.IsChecked == true)
            {
                multiBoxCheck(sender as TextBox);
            }
        }

        private void dnsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            multiBoxCheck(sender as TextBox);
        }

        private void dnsCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            dnsBox.BorderBrush = standardBorderBrush;
        }
    }
}
