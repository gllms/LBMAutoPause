using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        public System.Windows.Forms.NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            this.Hide();
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = new System.Drawing.Icon("pause.ico");
            notifyIcon.Text = "LBM Auto Pause";
            notifyIcon.Visible = true;
            notifyIcon.MouseUp += new MouseEventHandler((object sender, MouseEventArgs e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    notifyIcon.Visible = false;
                }
            });
            notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, exitClick);
        }

        private void exitClick(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Environment.Exit(0);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                this.Hide();
            notifyIcon.Visible = true;
            base.OnStateChanged(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            settings = new Settings(this);

            // source: https://social.msdn.microsoft.com/Forums/en-US/46f52ad5-2f97-4ad8-b95c-9e06705428bd/how-to-detect-lunch-or-closing-process-?forum=netfxbcl
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
            Nullable<bool> dialogResult = addDialog.ShowDialog();
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
            System.Diagnostics.Process.Start(path, "--stop");
            Log("LBM stopped");
        }

        public void StartLBM(string path)
        {
            System.Diagnostics.Process.Start(path, "--start");
            Log("LBM started");
        }

        public bool checkProcesses(List<string> p)
        {
            // Log(string.Join(',', Process.GetProcesses().Select((a) => a.ProcessName)));
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (p.Contains(Path.GetFileName(process.MainModule.FileName)))
                        return false;
                }
                catch
                {
                    // Log("unable to access " + process.ProcessName);
                }
            }
            return true;
            // return settings.p.Select((process) => Process.GetProcessesByName(process).Length == 0).All((b) => b);
        }

        public void Log(string input)
        {
            this.Dispatcher.Invoke(() =>
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
            this.Hide();
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
            if (File.Exists(this.filePath))
            {
                string[] settingsString = System.IO.File.ReadAllText(this.filePath).Split('\n');
                foreach (string e in settingsString[0].Split(','))
                {
                    if (e != "")
                        this.AddProcess(e);
                }
                this.LBMPath = settingsString[1];
                this.mainWindow.Log("Settings loaded from " + this.filePath);
            }
            if (!File.Exists(this.LBMPath))
            {
                Nullable<bool> dialogResult = false;
                LocateLBM locateLBMDialog = new LocateLBM();
                while (!File.Exists(locateLBMDialog.LBMPath))
                {
                    locateLBMDialog = new LocateLBM();
                    dialogResult = locateLBMDialog.ShowDialog();
                    if (dialogResult == false)
                    {
                        this.mainWindow.notifyIcon.Visible = false;
                        Environment.Exit(0);
                    }
                }
                this.LBMPath = locateLBMDialog.LBMPath;
                this.Save();
            }
            if (this.mainWindow.checkProcesses(this.p))
                this.mainWindow.StartLBM(this.LBMPath);
            else
                this.mainWindow.StopLBM(this.LBMPath);
        }

        public void AddProcess(string process)
        {
            this.p.Add(process);
            this.listBox.Items.Add(process);
            this.Save();
        }

        public void RemoveProcess(string process)
        {
            this.p.Remove(process);
            this.listBox.Items.Remove(listBox.SelectedItem);
            this.Save();
        }

        public void Save()
        {
            File.WriteAllText(this.filePath, String.Join(',', this.p) + "\n" + this.LBMPath);
        }
    }
}
