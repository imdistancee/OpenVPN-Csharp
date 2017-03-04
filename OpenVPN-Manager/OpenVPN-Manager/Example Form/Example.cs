using System;
using System.Windows.Forms;

namespace OpenVPN_Manager
{
    public partial class Example : Form
    {
        private OpenVPN vpn;
        private string OVPNFile { get; set; }

        public Example()
        {
            InitializeComponent();

            //this initializes the new OpenVPN variable.
            vpn = new OpenVPN();
            //this hooks the connection changed event.
            vpn.onConnectionChanged += Vpn_onConnectionChanged;
            //this hooks the status changed event.
            vpn.onStatusChanged += Vpn_onStatusChanged;
        }

        private void Vpn_onStatusChanged(string status)
        {
            /*
            NOTE:
                SOME STATUS STRINGS INCLUDE:
                    ~ connecting
                    ~ disconnected
                    ~ failed to connect
                    ~ checking logs
                    ~ openvpn process killed.
            */

            //this sets the status label using the status from the event.
            labelStatus.Invoke(new Action(() => labelStatus.Text = "Status: " + status));
        }

        private void Vpn_onConnectionChanged(bool connected)
        {
            //you can do whatever u want with this.
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "OVPN File (*.ovpn) | *.ovpn"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //this sets the public OVPNFile var to the filename you selected.
                OVPNFile = ofd.FileName;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //usage : user, pass, ovpnFile, logFile, authFile (optional)
            vpn.Connect(txtUser.Text, txtPass.Text, OVPNFile, "log.txt");
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            //this checks to see if you are connected to a server.
            if (vpn.Connected)
            {
                //this method disconnects from the openvpn server.
                vpn.Disconnect();
            }
        }
    }
}
