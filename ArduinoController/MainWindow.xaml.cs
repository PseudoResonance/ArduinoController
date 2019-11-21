using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ArduinoController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        public static MainWindow instance;

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
            Main.ReadSettings();
            var data = new Data();
            DataContext = data;
            instance.Controller.ItemsSource = joystickList;
            instance.Serial.ItemsSource = serialList;
            Main.PollSerial();
            pollThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (!shutdown)
                {
                    Main.PollJoysticks();
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
                Main.SetupPort();
            }
            else
            {
                Main.CloseSerial();
                Main.serialPort = null;
            }
        }

        private void Baud_FocusLost(object sender, RoutedEventArgs e)
        {
            String text = BaudRate.Text;
            if (text.Length > 0)
            {
                try
                {
                    Main.baudRate = int.Parse(text);
                    Main.SetupPort();
                }
                catch (Exception) { }
            }
        }

        private void Serial_Refresh(object sender, RoutedEventArgs e)
        {
            Main.PollSerial();
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
    }
}
