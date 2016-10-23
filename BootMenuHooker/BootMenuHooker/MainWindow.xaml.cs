using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace BootMenuHooker
{
    public partial class MainWindow : Window
    {
        #region Members

        private const uint WM_KEYDOWN = 0x100;
        private const uint WM_KEYUP = 0x101;

        private const int PID_UNSELECTED = -1;
        private const ConsoleKey CONSOLEKEY_UNSELECTED = ConsoleKey.NoName;
        private const string SENDKEY_UNELECTED = "__NOT_SELECTED__";

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly IList<string> targetKeyCandidateList = new List<string>
            {
                "Escape", "Delete",
                "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                "Ctrl+A", "Ctrl+B", "Ctrl+C", "Ctrl+D", "Ctrl+E", "Ctrl+F", "Ctrl+G", "Ctrl+H",
                "Ctrl+I", "Ctrl+J", "Ctrl+K", "Ctrl+L", "Ctrl+M", "Ctrl+N", "Ctrl+O", "Ctrl+P", "Ctrl+Q",
                "Ctrl+R", "Ctrl+S", "Ctrl+T", "Ctrl+U", "Ctrl+V", "Ctrl+W", "Ctrl+X", "Ctrl+Y", "Ctrl+Z",
            };

        private int targetPid = PID_UNSELECTED;
        private ConsoleKey targetConsoleKey = CONSOLEKEY_UNSELECTED;
        private string targetSendKey = SENDKEY_UNELECTED;

        private bool foregroundMode = false;
        private bool isExecuting = false;
        private bool isControlKeyNeeded = false;

        #endregion

        #region Initialization or Update for UI Component

        public MainWindow()
        {
            InitializeComponent();

            RefreshTargetWindowSelector();
            InitializeTargetKeySelector();

            UpdateUiComponentStatus();
        }

        private void RefreshTargetWindowSelector()
        {
            Process[] allCurrentProcesses = Process.GetProcesses();

            targetWindowSelector.Items.Clear();
            foreach (var eachProcess in allCurrentProcesses)
            {
                // Filtering out non-title processes
                if (string.IsNullOrEmpty(eachProcess.MainWindowTitle)) { continue; }

                targetWindowSelector.Items.Add(
                    convertProcessInformationIntoString(eachProcess.Id, eachProcess.ProcessName, eachProcess.MainWindowTitle)
                );
            }
        }

        private void InitializeTargetKeySelector()
        {
            foreach (var targetKeyCandidate in targetKeyCandidateList)
            {
                targetKeySelector.Items.Add(targetKeyCandidate);
            }
        }

        private void UpdateUiComponentStatus()
        {
            if (isExecuting) {
                startButton.IsEnabled = false;
                stopButton.IsEnabled = true;

                targetWindowLabel.IsEnabled = false;
                targetWindowSelector.IsEnabled = false;
                refreshButton.IsEnabled = false;
                targetKeyLabel.IsEnabled = false;
                targetKeySelector.IsEnabled = false;
                backgroundRadioButton.IsEnabled = false;
                foregroundRadioButton.IsEnabled = false;
                forceForegroundNote.IsEnabled = false;
            } else {
                startButton.IsEnabled = IsExecutionReady() ? true : false;
                stopButton.IsEnabled = false;

                targetWindowLabel.IsEnabled = true;
                targetWindowSelector.IsEnabled = true;
                refreshButton.IsEnabled = true;
                targetKeyLabel.IsEnabled = true;
                targetKeySelector.IsEnabled = true;
                backgroundRadioButton.IsEnabled = isControlKeyNeeded ? false : true;
                foregroundRadioButton.IsEnabled = isControlKeyNeeded ? false : true;
                forceForegroundNote.IsEnabled = true;
            }
        }

        #endregion

        #region Execution

        private bool IsExecutionReady()
        {
            return targetPid != PID_UNSELECTED &&
                (targetConsoleKey != CONSOLEKEY_UNSELECTED || targetSendKey != SENDKEY_UNELECTED);
        }

        private void Execute()
        {
            Task infiniteTask = Task.Factory.StartNew(() => {
                while (true)
                {
                    var hWnd = Process.GetProcessById(targetPid).MainWindowHandle;

                    if (foregroundMode)
                    {
                        pressKeyInForegroundMode(hWnd);
                    }
                    else
                    {
                        pressKeyInBackgroundMode(hWnd);
                    }

                    if (!isExecuting) { break; }
                    Thread.Sleep(1000);
                }
            });
        }

        private void pressKeyInForegroundMode(IntPtr hWnd)
        {
            SetForegroundWindow(hWnd);
            Thread.Sleep(100); // needed for window-switching time
            SendKeys.SendWait(targetSendKey);
        }

        private void pressKeyInBackgroundMode(IntPtr hWnd)
        {
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)targetConsoleKey, IntPtr.Zero);
        }

        #endregion

        #region Event Haldlers

        private void targetWindowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedWindow = targetWindowSelector.SelectedItem;
            if (selectedWindow != null) {
                setTargetPidBySelectedItem(selectedWindow.ToString());
            }
            else { targetPid = PID_UNSELECTED; } // case: after refreshed
            
            UpdateUiComponentStatus();
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTargetWindowSelector();
        }

        private void targetKeySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setTargetKeyBySelectedItem();
            UpdateUiComponentStatus();
        }

        private void backgroundRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            foregroundMode = false;
            setTargetKeyBySelectedItem();
        }

        private void foregroundRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            foregroundMode = true;
            setTargetKeyBySelectedItem();
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            isExecuting = true;
            UpdateUiComponentStatus();

            Execute();
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            isExecuting = false;
            UpdateUiComponentStatus();
        }

        #endregion

        #region Helper for Target Window

        private void setTargetPidBySelectedItem(string selectedItem)
        {
            var pid = selectedItem.Substring(selectedItem.LastIndexOf("[") + 1);
            pid = pid.Substring(0, pid.Length - 1);
            targetPid = int.Parse(pid);
        }

        private string convertProcessInformationIntoString(int pid, string name, string title)
        {
            return title + " (" + name + ") [" + pid + "]";
        }

        #endregion

        #region Helper for Target Key

        private void setTargetKeyBySelectedItem() {

            if (targetKeySelector.SelectedItem == null) { return; } // case: not selected yet

            string selectedItem = targetKeySelector.SelectedItem.ToString();

            if (selectedItem.Contains("Ctrl"))
            {
                foregroundMode = true;
                foregroundRadioButton.IsChecked = true;
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

                    default: targetSendKey = SENDKEY_UNELECTED; break;
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
                    default: targetConsoleKey = CONSOLEKEY_UNSELECTED; break;
                }
            }
        }

        #endregion
    }
}
