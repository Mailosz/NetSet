using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NetSet
{ 
    /*class DHCPServer
    {
        const int listenPort = 67;
        const int sendPort = 68;

        //public delegate void DHCPServerShutDown(DHCPServer server);
        //public event DHCPServerShutDown OnShutdown;

        NetDevice netDevice;
        Task listenTask;
        Task<UdpReceiveResult> receiveAction;
        CancellationTokenSource cancelToken = new CancellationTokenSource();
        UdpClient listener;
        IPAddress serverAddress;
        public bool IsRunning = false;
        public static int leaseTime = 3600;

        public byte[] ipv4Start = new byte[] { 192, 168, 1, 100 };
        public ulong maxNumber;

        public List<AssignedAddress> AddressList = new List<AssignedAddress>();

        public DHCPServer(NetDevice netDevice)
        {
            this.netDevice = netDevice;
        }

        public bool Establish(IPAddress address)
        {
            if (address != null)
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    try
                    {
                        serverAddress = address;
                        listener = new UdpClient(67);
                        listener.EnableBroadcast = true;
                        //listener.Connect(serverAddress, 67);

                        IsRunning = true;

                        listenTask = new Task(listen, cancelToken.Token);
                        listenTask.Start();

                        return true;
                    }
                    catch { return false; }
                }
                else return false;
            }
            else return false;
        }

        public void Shutdown()
        {
            IsRunning = false;
            if (cancelToken != null) cancelToken.Cancel();
        }

        async void listen()
        {
            receiveAction = listener.ReceiveAsync();
            while (IsRunning)
            {
                try
                {
                    receiveAction.Wait(cancelToken.Token);
                    var result = receiveAction.Result;
                    //rozpoczynanie kolejnego nasłuchu
                    receiveAction = listener.ReceiveAsync();

                    DHCPMessage message = DHCPMessage.FromBuffer(result.Buffer);
                    
                    if (message != null)
                    {

                        MessageBox.Show(message.ToString());
                        //dane
                        byte[] requestedIP = null;
                        byte type = 0;
                        string hostname;
                        bool[] parameters = new bool[255];
                        //odczyt mesejdża
                        foreach (var option in message.Options)
                        {
                            switch (option.Code)
                            {
                                case 12: //Host name
                                    StringBuilder sb = new StringBuilder(option.Length);
                                    foreach (var ch in option.Data) sb.Append((char)ch);
                                    hostname = sb.ToString();
                                    break;
                                case 50://Requested IP address
                                    if (option.Length == 4)
                                        requestedIP = option.Data;
                                    break;
                                case 51://IP address Lease Time
                                    break;
                                case 52://Option Overload
                                    break;
                                case 53://DHCP Message Type
                                    if (option.Length == 1)
                                        type = option.Data[0];
                                    break;
                                case 54://Server Identifier
                                    break;
                                case 55://Parameter Request List
                                    foreach (var b in option.Data) parameters[b] = true;
                                    break;
                                case 56://Message
                                    break;
                                case 57://Maximum DHCP Message Size
                                    break;
                                case 58://Renewal (T1) Time Value
                                    break;
                                case 59://Rebinding (T2) Time Value
                                    break;
                                case 60://Vendor class identifier
                                    break;
                                case 61://Client-identifier
                                    break;
                            }
                        }
                        //reakcja
                        switch (type)
                        {
                            case 1: //prośba o przydział IP
                                AssignedAddress assignedAddress = null;
                                //szukanie po adresie mac, czy już był przydzielony ten adres
                                foreach (var addr in AddressList)
                                {
                                    if (addr.Mac.SequenceEqual(message.Mac))
                                    {
                                        assignedAddress = addr;
                                        break;
                                    }
                                }

                                if (assignedAddress == null)//nie było w bazie, trzeba przeznaczyć nowy
                                {
                                    assignedAddress = new AssignedAddress(message.Mac);

                                    byte[] ip = (byte[])ipv4Start.Clone();
                                    bool used;
                                    ulong tries = 0;
                                    do
                                    {
                                        used = false;
                                        IPAddress address = new IPAddress(ipv4Start);

                                        foreach (var addr in AddressList)
                                        {
                                            if (addr.IP == address)
                                            {
                                                used = true;
                                                tries++;
                                                for (int i = ip.Length - 1; i >= 0; i--)
                                                {
                                                    if (ip[i] == 255) ip[i] = 0;
                                                    else ip[i]++;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    while (used && tries < maxNumber);
                                    //brak już wolnych, sprawdzanie czy nie ma jakiegoś, który można by zwolnić
                                    if (used)
                                    {
                                        foreach (var addr in AddressList)
                                        {
                                            if (DateTime.Now > addr.Expires)
                                            {
                                                ip = addr.IP.GetAddressBytes();
                                                AddressList.Remove(addr);
                                                used = false;
                                                break;
                                            }
                                        }
                                    }
                                    //nie dało się
                                    if (used) throw new Exception("Nie da się przypisać adresu IP do nowego urządzenia (Mac: " + message.Mac.ToString() + ")");

                                    //dało się
                                    assignedAddress.IP = new IPAddress(ip);

                                    AddressList.Add(assignedAddress);
                                }

                                DHCPMessage offer = new DHCPMessage();
                                offer.OP = 0x02;
                                offer.HTYPE = 0x01;
                                offer.HLEN = message.HLEN;
                                offer.HOPS = 0x00;

                                offer.YourIP = assignedAddress.IP.GetAddressBytes();
                                offer.ServerIP = serverAddress.GetAddressBytes();
                                offer.Mac = message.Mac;
                                offer.Cookie = message.Cookie;

                                offer.Options.Add(new DHCPOption(53, 2));
                                offer.Options.Add(new DHCPOption(50, assignedAddress.IP.GetAddressBytes()));
                                //dodatkowe dane w zależności od parametersów
                                if (parameters[3])//requested router
                                {
                                    var gateopt = new DHCPOption(3);
                                    foreach (var gate in netDevice.nic.GetIPProperties().GatewayAddresses)
                                    {
                                        if (gate.Address.AddressFamily == AddressFamily.InterNetwork)
                                        {
                                            var data = gateopt.Data.ToList();
                                            data.AddRange(gate.Address.GetAddressBytes());
                                            gateopt.Data = data.ToArray();
                                        }
                                    }
                                    gateopt.Length = (byte)gateopt.Data.Length;
                                    offer.Options.Add(gateopt);
                                }
                                if (parameters[1])//requested mask
                                    offer.Options.Add(new DHCPOption(1, 255, 255, 255, 0));
                                

                                //wysyłanie
                                byte[] respbytes = offer.ToBytes();
                                MessageBox.Show("Wysyłanie OFFER:\n\n" + offer.ToString());
                                await listener.SendAsync(respbytes, respbytes.Length, new IPEndPoint(IPAddress.Broadcast, 68));

                                break;

                            case 3:// DHCP request - potwierdzenie przyjęcia
                                if (message.ServerIP == serverAddress.GetAddressBytes())
                                {
                                    if (requestedIP != null)
                                    {
                                        foreach (var addr in AddressList)
                                        {
                                            if (addr.IP.GetAddressBytes() == requestedIP)
                                            {
                                                if (addr.Mac == message.Mac)
                                                {

                                                    //wszystko się zgadza, potwierdzajmy
                                                    DHCPMessage DHCPACK = new DHCPMessage();
                                                    DHCPACK.OP = 0x02;
                                                    DHCPACK.HTYPE = 0x01;
                                                    DHCPACK.HLEN = message.HLEN;
                                                    DHCPACK.HOPS = 0x00;

                                                    DHCPACK.YourIP = requestedIP;
                                                    DHCPACK.ServerIP = serverAddress.GetAddressBytes();
                                                    DHCPACK.Mac = message.Mac;
                                                    DHCPACK.Cookie = message.Cookie;

                                                    DHCPACK.Options.Add(new DHCPOption(53, 5));
                                                    DHCPACK.Options.Add(new DHCPOption(51, BitConverter.GetBytes(leaseTime)));
                                                    DHCPACK.Options.Add(new DHCPOption(54, serverAddress.GetAddressBytes()));

                                                    MessageBox.Show("Wysyłanie ACK:\n\n" + DHCPACK.ToString());
                                                    byte[] bytesresp = DHCPACK.ToBytes();
                                                    await listener.SendAsync(bytesresp, bytesresp.Length, new IPEndPoint(IPAddress.Broadcast, 68));
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
                catch
                {
                    listener.Close();
                    return;
                }
            }
        }
  
    }

    class DHCPMessage
    {
        public byte OP;
        public byte HTYPE;
        public byte HLEN;
        public byte HOPS;
        static public byte[] XID = new byte[4] { 0x39, 0x03, 0xF3, 0x26 };
        public byte[] SECS = new byte[2];
        public byte[] FLAGS = new byte[2];
        public byte[] ClientIP = new byte[4];//Client IP address
        public byte[] YourIP = new byte[4];//Your IP address
        public byte[] ServerIP = new byte[4];//Server IP address
        public byte[] GateIP = new byte[4];//Gateway IP address
        public byte[] Mac = new byte[16];//Client MAC address
        //192 bytes of space
        public byte[] Cookie = new byte[4];
        public List<DHCPOption> Options = new List<DHCPOption>();

        public static DHCPMessage FromBuffer(byte[] buffer)
        {
            if (buffer.Length < 240) return null;
            DHCPMessage mes = new DHCPMessage();
            
            mes.OP = buffer[0];
            mes.HTYPE = buffer[1];
            mes.HLEN = buffer[2];
            mes.HOPS = buffer[3];

            var list = buffer.ToList();
            //mes.XID = list.GetRange(4, 4).ToArray();
            mes.SECS = list.GetRange(8, 2).ToArray();
            mes.FLAGS = list.GetRange(10, 2).ToArray();
            mes.ClientIP = list.GetRange(12, 4).ToArray();//Client IP address
            mes.YourIP = list.GetRange(16, 4).ToArray();//Your IP address
            mes.ServerIP = list.GetRange(20, 4).ToArray();//Server IP address
            mes.GateIP = list.GetRange(24, 4).ToArray();//Gateway IP address
            mes.Mac = list.GetRange(28, mes.HLEN).ToArray();//Client MAC address
            mes.Cookie = list.GetRange(236, 4).ToArray();

            //optionsy
            int pos = 240;
            while (pos + 1 < buffer.Length)
            {
                DHCPOption option = new DHCPOption();
                option.Code = buffer[pos++];
                option.Length = buffer[pos++];
                if (pos + option.Length <= buffer.Length)
                {
                    option.Data = list.GetRange(pos, option.Length).ToArray();
                    pos += option.Length;
                    mes.Options.Add(option);
                }
                else break;
            }
            return mes;
        }

        public byte[] ToBytes()
        {
            int len = 240;
            foreach (var opt in Options)
            {
                len += opt.Length + 2;
            }

            byte[] bytes = new byte[len];

            bytes[0] = OP;
            bytes[1] = HTYPE;
            bytes[2] = HLEN;
            bytes[3] = HOPS;
            XID.CopyTo(bytes, 4);
            SECS.CopyTo(bytes, 2); ;
            FLAGS.CopyTo(bytes, 2);
            ClientIP.CopyTo(bytes, 4);
            YourIP.CopyTo(bytes, 4);
            ServerIP.CopyTo(bytes, 4);
            GateIP.CopyTo(bytes, 4);
            Mac.CopyTo(bytes, 16);

            Cookie.CopyTo(bytes, 236);
            int pos = 240;
            foreach (var opt in Options)
            {
                bytes[pos] = opt.Code;
                bytes[pos + 1] = opt.Length;
                opt.Data.CopyTo(bytes, pos + 2);
                pos += opt.Data.Length + 2;
            }

            return bytes;

        }

        public override string ToString()
        {
            string opts = "";
            foreach (var opt in Options)
            {
                opts += "CODE: " + opt.Code.ToString() + ", length:" + opt.Length.ToString() + ", data=" + string.Join(" ", opt.Data) + "\n";
            }
            return  
                    "Mac: " + string.Join("-", this.Mac) + "\n" +
                    "GateIP: " + new IPAddress(this.GateIP).ToString() + "\n" +
                    "ServerIP: " + new IPAddress(this.ServerIP).ToString() + "\n" +
                    "ClientIP: " + new IPAddress(this.ClientIP).ToString() + "\n" +
                    "Cookie: " + string.Join("-", this.Cookie) + "\n" +
                    "\n\nOptions:\n" + opts;
        }
    }

    class DHCPOption
    {
        public byte Code;
        public byte Length;
        public byte[] Data;

        public DHCPOption() { }

        public DHCPOption(byte code, params byte[] data)
        {
            Code = code;
            Length = (byte)data.Length;
            Data = data;
        }
    }

    class AssignedAddress
    {
        public IPAddress IP;
        public byte[] Mac;
        public bool Accepted;
        public DateTime Assigned;
        public DateTime Expires;

        public AssignedAddress(byte[] mac)
        {
            Mac = mac;
            Accepted = false;
            Assigned = DateTime.Now;
            Expires = DateTime.Now.AddSeconds(DHCPServer.leaseTime);
        }
    }
    */

    /*public static bool Establish()
        {
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                UdpClient listener = new UdpClient(listenPort);
                IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, listenPort);

                UdpState state = new UdpState();
                state.EndPoint = groupEP;
                state.Listener = listener;

                listener.BeginReceive(ReceiveMessage, state);

                return true;
            }
            else return false;
        }

        public static void ReceiveMessage(IAsyncResult ar)
        {
            UdpClient listener = (UdpClient)((UdpState)(ar.AsyncState)).Listener;
            IPEndPoint ep = (IPEndPoint)((UdpState)(ar.AsyncState)).EndPoint;

            Byte[] receiveBytes = listener.EndReceive(ar, ref ep);
            string receiveString = Encoding.ASCII.GetString(receiveBytes);

            Console.WriteLine("Received: {0}", receiveString);
            listener.BeginReceive(ReceiveMessage, state);
        }*/
}
