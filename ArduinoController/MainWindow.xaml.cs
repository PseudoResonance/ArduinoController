using Microsoft.Win32;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Management;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArduinoController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        public static MainWindow instance;

        private static readonly Regex nonNumericRegex = new Regex("[^0-9]+");

        public static WindowsTheme currentTheme = WindowsTheme.Light;

        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private const string RegistryValueName = "AppsUseLightTheme";

        public List<String> joystickList = new List<String>();

        public List<String> serialList = new List<String>();

        public static Controller controllerOne { get; private set; } = new Controller(UserIndex.One);
        public static Controller controllerTwo { get; private set; } = new Controller(UserIndex.Two);
        public static Controller controllerThree { get; private set; } = new Controller(UserIndex.Three);
        public static Controller controllerFour { get; private set; } = new Controller(UserIndex.Four);

        public static Boolean shutdown { get; private set; } = false;

        public static Thread pollThread;
        public static Thread joystickUpdateThread;
        public static Thread testPortThread;

        public static Boolean showDebug { get; private set; } = false;

        public MainWindow()
        {
            instance = this;
            InitializeComponent();
            WatchTheme();
            Main.ReadSettings();
            var data = new WindowData();
            DataContext = data;
            instance.Controller.ItemsSource = joystickList;
            instance.Serial.ItemsSource = serialList;
            ThreadPool.SetMaxThreads(1, 0);
            ThreadPool.QueueUserWorkItem(new WaitCallback(Initialize));
        }

        private void Initialize(Object state)
        {
            pollThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (!shutdown)
                {
                    Main.PollJoysticks();
                    Main.PollSerial();
                    Thread.Sleep(5000);
                }
            });
            pollThread.Start();
            joystickUpdateThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (!shutdown)
                {
                    Main.UpdateJoystick();
                    Thread.Sleep(Main.updateDelay);
                }
            });
            joystickUpdateThread.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            shutdown = true;
            Main.SaveSettings();
        }

        private void Controller_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Controller.SelectedItem != null)
            {
                String name = Controller.SelectedItem.ToString();
                if (name.Equals("One"))
                    Main.joystick = controllerOne;
                else if (name.Equals("Two"))
                    Main.joystick = controllerTwo;
                else if (name.Equals("Three"))
                    Main.joystick = controllerThree;
                else if (name.Equals("Four"))
                    Main.joystick = controllerFour;
            }
        }

        private void Serial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Serial.SelectedItem != null)
            {
                String name = Serial.SelectedItem.ToString();
                Main.serialName = name.Split(' ')[0];
                ThreadPool.QueueUserWorkItem(new WaitCallback(Main.InitializeSerial));
            }
            else
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(Main.CloseSerial));
            }
        }

        private void Baud_FocusLost(object sender, RoutedEventArgs e)
        {
            String text = BaudRate.Text;
            if (text.Length > 0)
            {
                try
                {
                    int newBaudRate = int.Parse(text);
                    if (newBaudRate != Main.baudRate)
                    {
                        Main.baudRate = newBaudRate;
                        ThreadPool.QueueUserWorkItem(new WaitCallback(Main.InitializeSerial));
                    }
                }
                catch (Exception) { }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Main.SaveSettings();
        }

        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            showDebug = ShowDebug.IsChecked;
            DebugOutput.Visibility = showDebug ? Visibility.Visible : Visibility.Hidden;
            DebugOutputLabel.Visibility = showDebug ? Visibility.Visible : Visibility.Hidden;
        }

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(WindowsTheme.Light);
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(WindowsTheme.Dark);
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Grid.Focus();
            Console.WriteLine("Focus");
        }

        private void TestNumericText(object sender, TextCompositionEventArgs e)
        {
            e.Handled = nonNumericRegex.IsMatch(e.Text);
        }

        private void TestNumericPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (nonNumericRegex.IsMatch(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {

        }

        public void SetTheme(WindowsTheme theme)
        {
            if (theme == WindowsTheme.Light)
            {
                this.DarkTheme.IsChecked = false;
                this.LightTheme.IsChecked = true;
            } else if (theme == WindowsTheme.Dark)
            {
                this.DarkTheme.IsChecked = true;
                this.LightTheme.IsChecked = false;
            }
            currentTheme = theme;
            Console.WriteLine("Using theme: " + theme);
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri($"/Themes/{theme}.xaml", UriKind.Relative);
        }

        public void WatchTheme()
        {
            var currentUser = WindowsIdentity.GetCurrent();
            string query = string.Format(
                CultureInfo.InvariantCulture,
                @"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{0}\\{1}' AND ValueName = '{2}'",
                currentUser.User.Value,
                RegistryKeyPath.Replace(@"\", @"\\"),
                RegistryValueName);

            try
            {
                var watcher = new ManagementEventWatcher(query);
                watcher.EventArrived += (sender, args) =>
                {
                    SetTheme(GetWindowsTheme());
                };

                // Start listening for events
                watcher.Start();
            }
            catch (Exception)
            {
                // This can fail on Windows 7
            }
            SetTheme(GetWindowsTheme());
        }

        private static WindowsTheme GetWindowsTheme()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                object registryValueObject = key?.GetValue(RegistryValueName);
                if (registryValueObject == null)
                {
                    return WindowsTheme.Light;
                }

                int registryValue = (int)registryValueObject;

                return registryValue > 0 ? WindowsTheme.Light : WindowsTheme.Dark;
            }
        }

        public enum WindowsTheme
        {
            Light,
            Dark
        }
    }
}