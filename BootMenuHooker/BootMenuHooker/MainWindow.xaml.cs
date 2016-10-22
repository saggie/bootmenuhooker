using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BootMenuHooker
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_KEYDOWN = 0x100;
        private const uint WM_KEYUP   = 0x101;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private IList<TargetWindowCandidate> targetWindowCandidateList = new List<TargetWindowCandidate> { };
        private IList<string> targetKeyCandidateList = new List<string>
            {
                "Escape", "Delete",
                "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                "Ctrl-A", "Ctrl-B", "Ctrl-C", "Ctrl-D", "Ctrl-E", "Ctrl-F", "Ctrl-G", "Ctrl-H",
                "Ctrl-I", "Ctrl-J", "Ctrl-K", "Ctrl-L", "Ctrl-M", "Ctrl-N", "Ctrl-O", "Ctrl-P", "Ctrl-Q",
                "Ctrl-R", "Ctrl-S", "Ctrl-T", "Ctrl-U", "Ctrl-V", "Ctrl-W", "Ctrl-X", "Ctrl-Y", "Ctrl-Z",
            };

        private int targetPid = -1;
        private ConsoleKey targetConsoleKey = ConsoleKey.NoName;
        private string targetSendKey = "";
        private bool isControlKeyNeeded = false;

        private bool isRunning = false;
        private bool foregroundMode = false;

        public MainWindow()
        {
            InitializeComponent();

            RefreshTargetWindowSelector();
            InitializeTargetKeySelector();
            UpdateIsEnabledStatus();
        }

        private void InitializeTargetKeySelector()
        {
            foreach (var targetKeyCandidate in targetKeyCandidateList)
            {
                targetKeySelector.Items.Add(targetKeyCandidate);
            }
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTargetWindowSelector();
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            isRunning = true;
            UpdateIsEnabledStatus();

            PressKey();
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            isRunning = false;
            UpdateIsEnabledStatus();
        }

        private void UpdateIsEnabledStatus()
        {
            if (isRunning) {
                startButton.IsEnabled = false;
                stopButton.IsEnabled = true;

                targetWindowLabel.IsEnabled = false;
                targetWindowSelector.IsEnabled = false;
                targetKeyLabel.IsEnabled = false;
                targetKeySelector.IsEnabled = false;
                refreshButton.IsEnabled = false;
                forceForegroundCheckBox.IsEnabled = false;
                forceForegroundLabel.IsEnabled = false;
            } else {
                startButton.IsEnabled = IsReady() ? true : false;
                stopButton.IsEnabled = false;

                targetWindowLabel.IsEnabled = true;
                targetWindowSelector.IsEnabled = true;
                targetKeyLabel.IsEnabled = true;
                targetKeySelector.IsEnabled = true;
                refreshButton.IsEnabled = true;
                forceForegroundCheckBox.IsEnabled = isControlKeyNeeded ? false : true;
                forceForegroundLabel.IsEnabled = true;
            }
        }

        private bool IsReady() {
            return targetPid != -1 && (targetConsoleKey != ConsoleKey.NoName || targetSendKey != "");
        }

        private void PressKey()
        {
            var hWnd = Process.GetProcessById(targetPid).MainWindowHandle;

            if (foregroundMode)
            {
                SetForegroundWindow(hWnd);
                Thread.Sleep(100); // needed for window-switching time
                SendKeys.SendWait(targetSendKey);
            }
            else
            {
                PostMessage(hWnd, WM_KEYDOWN, (IntPtr)targetConsoleKey, IntPtr.Zero);
            }
        }

        private void RefreshTargetWindowSelector()
        {
            Process[] currentProcesses = Process.GetProcesses();

            targetWindowCandidateList.Clear();
            foreach (var process in currentProcesses)
            {
                if (string.IsNullOrEmpty(process.MainWindowTitle)) { continue; }
                targetWindowCandidateList.Add(new TargetWindowCandidate(process.Id, process.ProcessName, process.MainWindowTitle));
            }

            // Update ComboBox Items
            targetWindowSelector.Items.Clear();
            foreach (var eachItem in targetWindowCandidateList) {
                targetWindowSelector.Items.Add(eachItem.toString());
            }
        }

        private void targetWindowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedWindow = targetWindowSelector.SelectedItem;
            if (selectedWindow != null) {
                setTargetPidBySelectedItem(selectedWindow.ToString());
            }
            else { targetPid = -1; }
            
            UpdateIsEnabledStatus();
        }

        private void setTargetPidBySelectedItem(string selectedItem)
        {
            var pid = selectedItem.Substring(selectedItem.LastIndexOf("[") + 1);
            pid = pid.Substring(0, pid.Length - 1);
            targetPid = int.Parse(pid);
        }

        private void targetKeySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setTargetKeyFromSelectedItem();
            UpdateIsEnabledStatus();
        }

        private void forceForegroundCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foregroundMode = true;
            setTargetKeyFromSelectedItem();
        }

        private void forceForegroundCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foregroundMode = false;
            setTargetKeyFromSelectedItem();
        }

        private void setTargetKeyFromSelectedItem() {

            string selectedItem = targetKeySelector.SelectedItem.ToString();

            if (selectedItem.Contains("Ctrl"))
            {
                foregroundMode = true;
                forceForegroundCheckBox.IsChecked = true;
                isControlKeyNeeded = true;
            }
            else
            {
                isControlKeyNeeded = false;
            }

            if (foregroundMode)
            {
                // function keys
                if (selectedItem.StartsWith("F")) {
                    targetSendKey = "{" + selectedItem + "}";
                    return;
                }

                // with Control keys
                if (selectedItem.StartsWith("Ctrl")) {
                    targetSendKey = "^" + selectedItem.Substring(selectedItem.Length - 1).ToLower();
                    return;
                }

                // other keys
                switch (selectedItem)
                {
                    case "Escape": targetSendKey = "{ESC}"; break;
                    case "Delete": targetSendKey = "{DEL}"; break;
                    default: targetSendKey = ""; break;
                }
            }
            else
            {
                switch (selectedItem)
                {
                    case "Escape": targetConsoleKey = ConsoleKey.Escape; break;
                    case "Delete": targetConsoleKey = ConsoleKey.Delete; break;
                    case "F1": targetConsoleKey = ConsoleKey.F1; break;
                    case "F2": targetConsoleKey = ConsoleKey.F2; break;
                    case "F3": targetConsoleKey = ConsoleKey.F3; break;
                    case "F4": targetConsoleKey = ConsoleKey.F4; break;
                    case "F5": targetConsoleKey = ConsoleKey.F5; break;
                    case "F6": targetConsoleKey = ConsoleKey.F6; break;
                    case "F7": targetConsoleKey = ConsoleKey.F7; break;
                    case "F8": targetConsoleKey = ConsoleKey.F8; break;
                    case "F9": targetConsoleKey = ConsoleKey.F9; break;
                    case "F10": targetConsoleKey = ConsoleKey.F10; break;
                    case "F11": targetConsoleKey = ConsoleKey.F11; break;
                    case "F12": targetConsoleKey = ConsoleKey.F12; break;
                    default: targetConsoleKey = ConsoleKey.NoName; break;
                }
            }
        }

        class TargetWindowCandidate
        {
            public int pid;
            public string name;
            public string title;
            public TargetWindowCandidate(int pid, string name, string title)
            {
                this.pid = pid;
                this.name = name;
                this.title = title;
            }
            public string toString()
            {
                return title + " (" + name + ") [" + pid + "]";
            }
        }
    }
}
