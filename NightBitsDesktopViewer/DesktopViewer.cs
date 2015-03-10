using NightBitsNetwork.Streaming;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace NightBits.DesktopViewer
{
    public partial class DesktopViewer : Form
    {
        private StreamingImageServer _server;
        private readonly int _serverPort = 8080;
        private readonly string _serverFormat = "http://{0}:{1}";
        private readonly int _linkLabelLocationX = 111;
        private int _linkLabelLocationY = 34;
        private int _width = Screen.PrimaryScreen.Bounds.Width;
        private int _height = Screen.PrimaryScreen.Bounds.Width;
        private readonly bool _showCursor = true;

        public DesktopViewer()
        {
            InitializeComponent();
            GetIpAddress();
           
            widthTextBox.Text = Screen.PrimaryScreen.Bounds.Width.ToString();
            heightTextBox.Text = Screen.PrimaryScreen.Bounds.Height.ToString();
        }

        private void GetIpAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    foreach (var ip in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            Console.WriteLine(ip.Address);
                            var linkLabel = new LinkLabel
                            {
                                Text = string.Format(_serverFormat, ip.Address, _serverPort),
                                Location = new Point(_linkLabelLocationX, _linkLabelLocationY),
                                AutoSize = true
                            };
                            _linkLabelLocationY += 25;
                            linkLabel.MouseDown += OnServerLinkLabelMouseDownEvent;

                            Controls.Add(linkLabel);
                        }
                    }
                }
            }
        }

        private void OnServerLinkLabelMouseDownEvent(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Process.Start(((LinkLabel)sender).Text);
            }
            else if (e.Button == MouseButtons.Right)
            {
                Clipboard.SetText(((LinkLabel) sender).Text);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var count = (_server.Clients != null) ? _server.Clients.Count() : 0;
            clientsLabel.Text = @"Clients: " + count;
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (_server != null && _server.IsRunning)
            {
                timer1.Enabled = false;
                _server.Stop();
                startButton.Text = @"Start";
            }
            else
            {
                _server = new StreamingImageServer(_width, _height, _showCursor);
                _server.SetLogBook(logBook);
                _server.Start(_serverPort);
                timer1.Enabled = true;

                startButton.Text = @"Stop";
            }
        }

        private void heightTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void widthTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void widthTextBox_TextChanged(object sender, EventArgs e)
        {
            _width = Convert.ToInt32(widthTextBox.Text);
        }

        private void heightTextBox_TextChanged(object sender, EventArgs e)
        {
            _height = Convert.ToInt32(heightTextBox.Text);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(@"Created and copyrighted by: NightBits (Jeroen Saey)", @"About");
        }
    }
}