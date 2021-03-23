using System;
using System.Collections.Generic;
using System.Windows;
using System.Management;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace LBMAutoPause
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Settings settings;
        public ManagementEventWatcher startWatch;
        public ManagementEventWatcher stopWatch;
        public NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            Hide();
            notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("pause.ico"),
                Text = "LBM Auto Pause",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (object sender, EventArgs e) =>
            {
                notifyIcon.Visible = false;
                Environment.Exit(0);
            });
            notifyIcon.MouseUp += new MouseEventHandler((object sender, MouseEventArgs e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    notifyIcon.Visible = false;
                }
            });
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide();
            notifyIcon.Visible = true;
            base.OnStateChanged(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            settings = new Settings(this);
            startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += new EventArrivedEventHandler(startWatch_EventArrived);
            startWatch.Start();
            stopWatch = new ManagementEventWatcher(
              new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived += new EventArrivedEventHandler(stopWatch_EventArrived);
            stopWatch.Start();
        }

        private void stopWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string stoppedProcess = (string)e.NewEvent.Properties["ProcessName"].Value;
            if (settings.p.Contains(stoppedProcess))
            {
                Log("Process stopped: " + stoppedProcess);
                if (checkProcesses(settings.p))
                {
                    StartLBM(settings.LBMPath);
                }
                else
                {
                    Log("Matching processes found, not started\n");
                }
            }
        }

        private void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string startedProcess = (string)e.NewEvent.Properties["ProcessName"].Value;
            if (settings.p.Contains(startedProcess))
            {
                Log("Process started: " + e.NewEvent.Properties["ProcessName"].Value);
                if (!checkProcesses(settings.p))
                {
                    StopLBM(settings.LBMPath);
                }
                else
                {
                    Log("No matching processes found, not stopped");
                }
            }
        }

        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            AddDialog addDialog = new AddDialog();
            bool? dialogResult = addDialog.ShowDialog();
            if (dialogResult == true)
            {
                settings.AddProcess(addDialog.Process);
                if (Process.GetProcessesByName(addDialog.Process).Length > 0)
                {
                    StopLBM(settings.LBMPath);
                }
            }
        }

        private void buttonRemove_Click(object sender, RoutedEventArgs e)
        {
            settings.RemoveProcess((string)listBox.SelectedItem);
        }

        public void StopLBM(string path)
        {
            Process.Start(path, "--stop");
            Log("LBM stopped");
        }

        public void StartLBM(string path)
        {
            Process.Start(path, "--start");
            Log("LBM started");
        }

        public bool checkProcesses(List<string> p)
        {
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (p.Contains(Path.GetFileName(process.MainModule.FileName)))
                        return false;
                } catch {}
            }
            return true;
        }

        public void Log(string input)
        {
            Dispatcher.Invoke(() =>
              {
                  textBlock.Text += input + "\n";
                  scrollViewer.ScrollToBottom();
              });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            startWatch.Stop();
            stopWatch.Stop();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            notifyIcon.Visible = true;
            e.Cancel = true;
        }
    }

    public class Settings
    {
        public List<string> p = new List<string>();

        public string filePath = Path.Combine(Directory.GetCurrentDirectory(), @"settings.txt");
        public string LBMPath = @"C:\Program Files\LittleBigMouse\LittleBigMouse_Daemon.exe";
        private MainWindow mainWindow;
        public System.Windows.Controls.ListBox listBox;

        public Settings(MainWindow m)
        {
            mainWindow = m;
            listBox = m.listBox;
            if (File.Exists(filePath))
            {
                string[] settingsString = File.ReadAllText(filePath).Split('\n');
                foreach (string e in settingsString[0].Split(','))
                    if (e != "") AddProcess(e);
                LBMPath = settingsString[1];
                mainWindow.Log("Settings loaded from " + filePath);
            }
            if (!File.Exists(LBMPath))
            {
                LocateLBM locateLBMDialog = new LocateLBM();
                while (!File.Exists(locateLBMDialog.LBMPath))
                {
                    locateLBMDialog = new LocateLBM();
                    if (locateLBMDialog.ShowDialog() == false)
                    {
                        mainWindow.notifyIcon.Visible = false;
                        Environment.Exit(0);
                    }
                }
                LBMPath = locateLBMDialog.LBMPath;
                Save();
            }
            if (mainWindow.checkProcesses(p))
                mainWindow.StartLBM(LBMPath);
            else
                mainWindow.StopLBM(LBMPath);
        }

        public void AddProcess(string process)
        {
            p.Add(process);
            listBox.Items.Add(process);
            Save();
        }

        public void RemoveProcess(string process)
        {
            p.Remove(process);
            listBox.Items.Remove(listBox.SelectedItem);
            Save();
        }

        public void Save()
        {
            File.WriteAllText(filePath, string.Join(',', p) + "\n" + LBMPath);
        }
    }
}
