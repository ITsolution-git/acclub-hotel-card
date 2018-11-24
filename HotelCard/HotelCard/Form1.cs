using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Quobject.SocketIoClientDotNet.Client;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace HotelCard
{

    public unsafe partial class Form1 : Form
    {
        private System.Windows.Forms.ContextMenu contextMenu1;
        private System.Windows.Forms.MenuItem menuItem1;
        private Thread readThread;

        private byte[] _cardno = new byte[6];
        private int readThreadTime = 2000;
        private string selectedCardNo;
        private int readStatus = -1;

        private String AUTH = "867554";
        private String DOMAIN = "http://localhost";
        private String WS_PORT = "31556";
        private String APP_PORT = "8069";
        private String HOTEL = "false";
        private String POS = "true";

        private String FOLIO_URL = "/web#id={0}&view_type=form&model=hotel.folio&menu_id=233&action=356";

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_connect(short beep);

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_disconnect();

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_read_card(string auth, byte[] cardno,
            byte[] building, byte[] room, byte[] commdoors, byte[] arrival, 
            byte[] departure);

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_write_card(string auth,
                                string building,
                                string room,
                                string commdoors,
                                string arrival,
                                string departure,
                                string suspendnum,
                                int mode);

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_get_card_number(out string cardno);

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_get_card_number(byte[] cardno);

        [DllImport("CLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short dv_delete_card(byte[] room);

        Socket client;
        public Form1()
        {
            InitializeComponent();
            InitializeClient();

            Hide();

            String configuration = System.IO.File.ReadAllText(@"config.json");
            JObject configure = JObject.Parse(configuration);
            Console.Write(configure);
            if (configure["AUTH"] != null && configure["AUTH"].ToString() != "")
                this.AUTH = configure["AUTH"].ToString();
            if (configure["DOMAIN"] != null && configure["DOMAIN"].ToString() != "")
                this.DOMAIN = configure["DOMAIN"].ToString();
            if (configure["WS_PORT"] != null && configure["WS_PORT"].ToString() != "")
                this.WS_PORT = configure["WS_PORT"].ToString();
            if (configure["APP_PORT"] != null && configure["APP_PORT"].ToString() != "")
                this.APP_PORT = configure["APP_PORT"].ToString();
            if (configure["HOTEL"] != null && configure["HOTEL"].ToString() != "")
                this.HOTEL = configure["HOTEL"].ToString();
            if (configure["POS"] != null && configure["POS"].ToString() != "")
                this.POS = configure["POS"].ToString();

            this.contextMenu1 = new System.Windows.Forms.ContextMenu();
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.contextMenu1.MenuItems.AddRange(
                    new System.Windows.Forms.MenuItem[] { this.menuItem1 });

            // Initialize menuItem1
            this.menuItem1.Index = 0;
            this.menuItem1.Text = "E&xit";
            this.menuItem1.Click += new System.EventHandler(this.menuItem1_Click);
            notifyIcon1.ContextMenu = this.contextMenu1;

            // thread start
            readThread = new Thread(new ThreadStart(DetectThreadStart));
            readThread.Start();

            UsbNotification.RegisterUsbDeviceNotification(this.Handle);
            dv_connect(1);
            dv_connect(1);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == UsbNotification.WmDevicechange)
            {
                switch ((int)m.WParam)
                {
                    case UsbNotification.DbtDeviceremovecomplete:
                        Console.WriteLine("Removed!"); // this is where you do your magic
                        break;
                    case UsbNotification.DbtDevicearrival:
                        Console.WriteLine("Usb Detected!"); // this is where you do your magic
                        Thread.Sleep(2000);
                        dv_connect(1);
                        dv_connect(1);
                        break;
                }
            }
        }

        void DetectThreadStart()
        {
            while(true)
            {
                Thread.Sleep(this.readThreadTime);
                Console.WriteLine("read!");
                var status = dv_get_card_number(this._cardno);
                if (status == 0)
                {
                    if(this.readStatus != 0)
                    {
                        this.selectedCardNo = Encoding.ASCII.GetString(_cardno);
                        
                        String[] card_data = readCard();
                        if (card_data == null)
                        {
                            notifyIcon1.BalloonTipText = "Cannot read card! It's not guest card.";
                            notifyIcon1.Visible = true;
                            notifyIcon1.ShowBalloonTip(1000);
                            return;
                        }
                        String payload = "{\"cardno\": \"" +
                            card_data[0] + "\", \"hotel\": \"" +
                            this.HOTEL + "\", \"pos\": \"" +
                            this.POS + "\"}";
                        Console.WriteLine(payload);
                        client.Emit("get_url", payload);
                    } else if(this.readStatus == status)
                    {
                        this.readThreadTime = 10000;
                    }
                    
                } else
                {
                    this.readThreadTime = 2000;
                }
                this.readStatus = status;
            }
        }

        private void InitializeClient()
        {
            client = IO.Socket(string.Format("{0}:{1}", DOMAIN, WS_PORT));
            client.On(Socket.EVENT_CONNECT, () =>
            {
            });
            client.On(Socket.EVENT_DISCONNECT, () =>
            {
            });

            client.On("issue_card", (data) =>
            {
                // write data into the card
                Console.WriteLine(data);
                var json = (JObject)data;

                // check whether prev card is existed or not
                var report_flag = 0;
                if (json["prev_cardno"] != null && json["prev_cardno"].ToString() != "")
                {
                    report_flag = 1;
                }

                var res = dv_write_card(this.AUTH, "01", json["product_id"][1].ToString(), "00",
                    json["checkin_date"].ToString(),
                    json["checkout_date"].ToString(), json["prev_cardno"].ToString(), report_flag);

                if (res == 0) // if writing data on card is success.
                {
                    // notification on window toolbar
                    notifyIcon1.BalloonTipText = "Issued a card on Room " + json["product_id"][1] + ".";
                    notifyIcon1.Visible = true;
                    notifyIcon1.ShowBalloonTip(1000);

                    // get card number
                    if (dv_get_card_number(this._cardno) == 0)
                    {
                        Console.WriteLine(Encoding.ASCII.GetString(this._cardno));
                        // call ws request for saving card number in database
                        String payload = "{ \"cardno\": \"" + 
                            Encoding.ASCII.GetString(this._cardno) + 
                            "\", \"product_no\": \"" +
                            json["product_id"][0].ToString() + "\" }";
                        client.Emit("write_cardno", payload);
                    }

                }
            });

            client.On("delete_card", (data) => {
                String[] card_data = readCard();
                if (card_data == null)
                {
                    notifyIcon1.BalloonTipText = "Removing a card is failed! (cannot read card)";
                    notifyIcon1.Visible = true;
                    notifyIcon1.ShowBalloonTip(1000);
                    return;
                }

                var json = (JObject)data;
                if ("0" + json["product_id"][1].ToString() != card_data[2])
                {
                    // notification on window toolbar
                    notifyIcon1.BalloonTipText = "The card is not assigned to Room " + 
                        json["product_id"][1] + ".";
                    notifyIcon1.Visible = true;
                    notifyIcon1.ShowBalloonTip(1000);
                    return;
                }
                byte[] _room = new byte[4];
                if (dv_delete_card(_room) == 0)
                {
                    notifyIcon1.BalloonTipText = "The card is removed from Room " +
                         Encoding.ASCII.GetString(_room) + " successfully.";
                    notifyIcon1.Visible = true;
                    notifyIcon1.ShowBalloonTip(1000);
                }

            });

            client.On("get_url", (data) => {
                var json = (JObject)data;
                Console.WriteLine(data);
                String url = string.Format(FOLIO_URL, json["folio_id"]);
                Process.Start("chrome.exe", string.Format("{0}:{1}", DOMAIN, APP_PORT) + url);
            });
        }

        private void connect_Click(object sender, EventArgs e)
        {
            dv_connect(1);
        }

        private void menuItem1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private String[] readCard()
        {
            byte[] _cardno = new byte[6];
            byte[] _building = new byte[2];
            byte[] _room = new byte[4];
            byte[] _commdoors = new byte[2];
            byte[] _arrival = new byte[19];
            byte[] _departure = new byte[19];

            var res = dv_read_card(this.AUTH, _cardno, _building,
                         _room, _commdoors, _arrival, _departure);
            if (res == 0)
                return new string[] { Encoding.ASCII.GetString(_cardno), Encoding.ASCII.GetString(_building),
                    Encoding.ASCII.GetString(_room), Encoding.ASCII.GetString(_commdoors),
                    Encoding.ASCII.GetString(_arrival), Encoding.ASCII.GetString(_departure)};
            else
                return null;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.BalloonTipText = "Starting Running ...";
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(1000);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            notifyIcon1.Visible = true;
            notifyIcon1.BalloonTipText = "Starting Program ...";
            notifyIcon1.ShowBalloonTip(1000);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
            }
        }
    }

    internal static class UsbNotification
    {
        public const int DbtDevicearrival = 0x8000; // system detected a new device        
        public const int DbtDeviceremovecomplete = 0x8004; // device is gone      
        public const int WmDevicechange = 0x0219; // device change event      
        private const int DbtDevtypDeviceinterface = 5;
        private static readonly Guid GuidDevinterfaceUSBDevice = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED"); // USB devices
        private static IntPtr notificationHandle;

        /// <summary>
        /// Registers a window to receive notifications when USB devices are plugged or unplugged.
        /// </summary>
        /// <param name="windowHandle">Handle to the window receiving notifications.</param>
        public static void RegisterUsbDeviceNotification(IntPtr windowHandle)
        {
            DevBroadcastDeviceinterface dbi = new DevBroadcastDeviceinterface
            {
                DeviceType = DbtDevtypDeviceinterface,
                Reserved = 0,
                ClassGuid = GuidDevinterfaceUSBDevice,
                Name = 0
            };

            dbi.Size = Marshal.SizeOf(dbi);
            IntPtr buffer = Marshal.AllocHGlobal(dbi.Size);
            Marshal.StructureToPtr(dbi, buffer, true);

            notificationHandle = RegisterDeviceNotification(windowHandle, buffer, 0);
        }

        /// <summary>
        /// Unregisters the window for USB device notifications
        /// </summary>
        public static void UnregisterUsbDeviceNotification()
        {
            UnregisterDeviceNotification(notificationHandle);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);

        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DevBroadcastDeviceinterface
        {
            internal int Size;
            internal int DeviceType;
            internal int Reserved;
            internal Guid ClassGuid;
            internal short Name;
        }
    }
}
    