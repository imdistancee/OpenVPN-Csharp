using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OpenVPN_Manager
{
    /// <summary>
    /// Connection changed event. Will use for handling connections.
    /// </summary>
    /// <param name="connected"></param>
    public delegate void ConnectionChangedEvent(bool connected);

    /// <summary>
    /// Connection status changed event. "connecting..." "connected" "disconnected" etc.
    /// </summary>
    /// <param name="status"></param>
    public delegate void ConnectionStatusChanged(string status);

    public class OpenVPN
    {
        /// <summary>
        /// OpenVPN executable path.
        /// </summary>
        private string OpenVpnPath = @"C:\Program Files\OpenVPN\bin\openvpn.exe";

        /// <summary>
        /// This is used for calling the connection changed delegate
        /// </summary>
        public event ConnectionChangedEvent onConnectionChanged;

        /// <summary>
        /// This is used for calling the ConnectionStatusChanged delegate
        /// </summary>
        public event ConnectionStatusChanged onStatusChanged;

        /// <summary>
        /// this checks to see if openvpn is connected or not.
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// this is used for getting logs.
        /// </summary>
        private string logPath { get; set; }

        /// <summary>
        /// these are the background workers for determining connection states.
        /// </summary>
        public BackgroundWorker logChecker, processChecker;

        /// <summary>
        /// OpenVPN Class Initializer.
        /// </summary>
        public OpenVPN()
        {
            //initializes background worker.
            logChecker = new BackgroundWorker();
            //makes it so the worker can be cancelled
            logChecker.WorkerSupportsCancellation = true;
            //hooks the dowork event
            logChecker.DoWork += LogChecker_DoWork;

            //initializes background worker.
            processChecker = new BackgroundWorker();
            //makes it so the worker can be cancelled
            processChecker.WorkerSupportsCancellation = true;
            //hooks the dowork event
            processChecker.DoWork += ProcessChecker_DoWork;
            //runs the background worker.
            processChecker.RunWorkerAsync();
        }


        /// <summary>
        /// this method is called whenever the process checker is doing an async operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            //this code runs while the worker is busy.
            while (processChecker.IsBusy)
            {
                //if the worker is pending cancellation
                if (processChecker.CancellationPending)
                {
                    //cancel the worker
                    e.Cancel = true;
                    //exit the method
                    return;
                }

                //checks to see if openvpn is connected
                if (Connected)
                {
                    //checks to see if there are no openvpn processes
                    if (Process.GetProcessesByName("openvpn").Length == 0)
                    {
                        //sets the connected variable to false
                        Connected = false;
                        //calls on connection changed event
                        onConnectionChanged(false);
                        //calls on status changed event
                        onStatusChanged("OpenVPN process killed");
                    }
                }
                //sleeps the thread for 1 second
                System.Threading.Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// this method is called whenever the log checker is doing an async operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            //calls the on status changed event.
            onStatusChanged("Checking Logs");
            //while server is not connected.
            while (!Connected)
            {
                //checks to see if the worker is pending cancellation.
                if (logChecker.CancellationPending)
                {
                    //cancels the worker.
                    e.Cancel = true;
                    //exits the method.
                    return;
                }

                //if get logs contains "Initialization Sequence Completed" (meaning server connected)
                if (GetLogs().Contains("Initialization Sequence Completed"))
                {
                    onConnectionChanged(true);
                    onStatusChanged("Connected");
                    Connected = true;
                }
                else
                {
                    //checks to see if there are no openvpn processes
                    if (Process.GetProcessesByName("openvpn").Length == 0)
                    {
                        //sets the connected variable to false
                        Connected = false;
                        //calls on connection changed event
                        onConnectionChanged(false);
                        //calls on status changed event
                        onStatusChanged("Failed to connect");
                        //cancels the workers operation.
                        logChecker.CancelAsync();
                    }
                }
            }

            //cancels the workers operation.
            logChecker.CancelAsync();
        }

        /// <summary>
        /// Connects using OpenVPN using the specified config path, username, and password. Also sets the log path.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="ovpnFile"></param>
        /// <param name="logPath"></param>
        public void Connect(string username, string password, string ovpnFilePath, string logPath, string authFile = "auth.txt")
        {
            //checks to see if you are currently connected.
            if (Connected)
            {
                //disconnects from the connected server.
                Disconnect();
            }

            //sets the global log path variable.
            this.logPath = logPath;

            //writes the username and password to the auth file.
            File.WriteAllText(authFile, $"{username}{Environment.NewLine}{password}");

            //creates a new process start info variable to store all the info we need to use openvpn.exe.
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = OpenVpnPath,
                Arguments = $"--config {ovpnFilePath} --auth-user-pass {authFile} --log {logPath}",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            //starts the openvpn.exe process using the psi variable
            Process.Start(startInfo);
            //calls the on status changed event.
            onStatusChanged("Connecting");

            //checks to see if the log checker is running.
            if (logChecker.IsBusy)
            {
                //stops the log checker from running.
                logChecker.CancelAsync();
            }

            //runs the log checker
            logChecker.RunWorkerAsync();
        }

        public void Disconnect()
        {
            //checks if open vpn is connected.
            if (Connected)
            {
                //checks to see if the log checker is running.
                if (logChecker.IsBusy)
                {
                    //stops the log checker from running.
                    logChecker.CancelAsync();
                }

                //loops through all the current processes
                foreach (Process process in Process.GetProcesses())
                {
                    //checks to see if the process name starts with "openvpn"
                    if (process.ProcessName.StartsWith("openvpn"))
                    {
                        //kills the current process.
                        process.Kill();
                    }
                }

                //calls the on status changed event.
                onStatusChanged("Disconnected");
                //calls the on connection changed event.
                onConnectionChanged(false);
                //sets connected to false.
                Connected = false;
            }
            else
            {
                //throws an exception if your not connected to a server.
                throw new Exception("You are not connected to a server.");
            }
        }

        /// <summary>
        /// this method gets the log file contents.
        /// </summary>
        /// <returns></returns>
        public string GetLogs()
        {
            //catches exceptions
            try
            {
                //checks to see if the file exists
                if (File.Exists(logPath))
                {
                    //initializes a filestream with read/write permissions
                    using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    //reads the file stream
                    using (var sr = new StreamReader(fs, Encoding.Default))
                    {
                        //returns the file contents
                        return sr.ReadToEnd();
                    }
                }
                else
                {
                    //returns empty if file doesn't exist.
                    return string.Empty;
                }
            }
            catch
            {
                //returns empty is error is thrown.
                return string.Empty;
            }
        }
    }
}
