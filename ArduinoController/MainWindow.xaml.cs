using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ArduinoController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {

        public static short deadzoneRadius = 3500;
        public static short updateDelay = 20;
        public static int baudRate = 9600;

        public static MainWindow instance;

        private static String joystickName = "";
        private Controller joystick = null;

        private Controller controllerOne = new Controller(UserIndex.One);
        private Controller controllerTwo = new Controller(UserIndex.Two);
        private Controller controllerThree = new Controller(UserIndex.Three);
        private Controller controllerFour = new Controller(UserIndex.Four);

        private List<String> joystickList = new List<String>();

        private static String serialName = "";
        private SerialPort serialPort = null;
        private Boolean isSerialReady = false;

        private List<String> serialList = new List<String>();

        private Thread pollThread;
        private Thread joystickUpdateThread;
        private Thread testPortThread;

        private Boolean shutdown = false;

        private static byte[] buffer = new byte[] { 67, 114, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public MainWindow()
        {
            instance = this;
            InitializeComponent();
            ReadSettings();
            var data = new Data();
            DataContext = data;
            instance.Controller.ItemsSource = joystickList;
            instance.Serial.ItemsSource = serialList;
            PollSerial();
            pollThread = new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                while (!shutdown)
                {
                    PollJoysticks();
                    Thread.Sleep(5000);
                }
            });
            pollThread.Start();
            joystickUpdateThread = new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                while (!shutdown)
                {
                    UpdateJoystick();
                    Thread.Sleep(updateDelay);
                }
            });
            joystickUpdateThread.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            shutdown = true;
            SaveSettings();
        }

        private void Controller_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Controller.SelectedItem != null)
            {
                String name = Controller.SelectedItem.ToString();
                if (name.Equals("One"))
                    joystick = controllerOne;
                else if (name.Equals("Two"))
                    joystick = controllerTwo;
                else if (name.Equals("Three"))
                    joystick = controllerThree;
                else if (name.Equals("Four"))
                    joystick = controllerFour;
            }
        }

        private void Serial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Serial.SelectedItem != null)
            {
                String name = Serial.SelectedItem.ToString();
                serialName = name;
                setupPort();
            }
            else
            {
                serialPort.Close();
                isSerialReady = false;
                serialPort = null;
            }
        }

        private void Baud_FocusLost(object sender, RoutedEventArgs e)
        {
            if (BaudRate.Text.Length > 0)
            {
                try
                {
                    baudRate = int.Parse(BaudRate.Text);
                    setupPort();
                } catch (Exception) {}
            }
        }

        private void Serial_Refresh(object sender, RoutedEventArgs e)
        {
            PollSerial();
        }

        private float calculateDeadzone(short n)
        {
            if (Math.Abs((int)n) <= deadzoneRadius)
                return 0;
            else
            {
                float c = ((float)(Math.Abs((int)n) - deadzoneRadius)) / (short.MaxValue - deadzoneRadius);
                if (c > 1) c = 1;
                return (n > 0) ? c : -c;
            }
        }

        private float calculatePercentage(byte n)
        {
            float c = (float)n / byte.MaxValue;
            if (c > 1)
                c = 1;
            else if (c < -1)
                c = -1;
            return c;
        }

        private void UpdateJoystick()
        {
            short leftX = 0;
            short leftY = 0;
            short rightX = 0;
            short rightY = 0;
            byte leftTrigger = 0;
            byte rightTrigger = 0;
            GamepadButtonFlags buttons = 0;
            try
            {
                if (joystick != null)
                {
                    var state = joystick.GetState();
                    leftX = state.Gamepad.LeftThumbX;
                    leftY = state.Gamepad.LeftThumbY;
                    rightX = state.Gamepad.RightThumbX;
                    rightY = state.Gamepad.RightThumbY;
                    leftTrigger = state.Gamepad.LeftTrigger;
                    rightTrigger = state.Gamepad.RightTrigger;
                    buttons = state.Gamepad.Buttons;
                }
            }
            catch (Exception) { }
            try
            {
                if (serialPort != null && serialPort.IsOpen && isSerialReady)
                {
                    byte[] tempArray = BitConverter.GetBytes(calculateDeadzone(leftX));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 2, 4);
                    tempArray = BitConverter.GetBytes(calculateDeadzone(leftY));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 6, 4);
                    tempArray = BitConverter.GetBytes(calculateDeadzone(rightX));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 10, 4);
                    tempArray = BitConverter.GetBytes(calculateDeadzone(rightY));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 14, 4);
                    tempArray = BitConverter.GetBytes(calculatePercentage(leftTrigger));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 18, 4);
                    tempArray = BitConverter.GetBytes(calculatePercentage(rightTrigger));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 22, 4);
                    tempArray = BitConverter.GetBytes((ushort) buttons);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 26, 2);

                    serialPort.Write(buffer, 0, buffer.Length);
                    String str = "";
                    foreach (byte b in buffer) {
                        str += b + " ";
                    }
                    this.Dispatcher.Invoke(() =>
                    {
                        DebugOutput.Text = str;
                    });
                } else if (testPortThread == null)
                {
                    testPortThread = new Thread(() =>
                    {
                        testPort();
                    });
                    testPortThread.Start();
                }
            }
            catch (IOException)
            {
                isSerialReady = false;
                if (testPortThread == null)
                {
                    testPortThread = new Thread(() =>
                    {
                        testPort();
                    });
                    testPortThread.Start();
                }
            }
        }

        private bool setupPort()
        {
            if (serialPort != null)
            {
                Console.WriteLine("Closing current port");
                serialPort.Close();
            }
            Console.WriteLine("Setting up Serial: " + serialName + " with baud rate: " + baudRate);
            try
            {
                serialPort = new SerialPort(serialName);
                serialPort.BaudRate = baudRate;
                serialPort.Open();
                isSerialReady = true;
                Console.WriteLine("Port open!");
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("Port failed to open!");
                isSerialReady = false;
                return false;
            }
        }

        private void testPort()
        {
            while (!shutdown && !isSerialReady)
            {
                if (setupPort())
                    break;
                Thread.Sleep(5000);
            }
            testPortThread = null;
        }

        private void PollJoysticks()
        {
            Boolean updated = false;

            if (controllerOne.IsConnected)
            {
                if (!joystickList.Contains("One"))
                {
                    joystickList.Add("One");
                    updated = true;
                }
            } else
            {
                if (joystickName.Equals("One"))
                    joystickName = "";
                if (joystickList.Contains("One"))
                {
                    joystickList.Remove("One");
                    updated = true;
                }
            }
            if (controllerTwo.IsConnected)
            {
                if (!joystickList.Contains("Two"))
                {
                    joystickList.Add("Two");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Two"))
                    joystickName = "";
                if (joystickList.Contains("Two"))
                {
                    joystickList.Remove("Two");
                    updated = true;
                }
            }
            if (controllerThree.IsConnected)
            {
                if (!joystickList.Contains("Three"))
                {
                    joystickList.Add("Three");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Three"))
                    joystickName = "";
                if (joystickList.Contains("Three"))
                {
                    joystickList.Remove("Three");
                    updated = true;
                }
            }
            if (controllerFour.IsConnected)
            {
                if (!joystickList.Contains("Four"))
                {
                    joystickList.Add("Four");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Four"))
                    joystickName = "";
                if (joystickList.Contains("Four"))
                {
                    joystickList.Remove("Four");
                    updated = true;
                }
            }
            if (updated)
            {
                this.Dispatcher.Invoke(() => {
                    instance.Controller.Items.Refresh();
                    if (joystickName.Equals(""))
                    {
                        if (controllerOne.IsConnected)
                            instance.Controller.SelectedItem = "One";
                        else if (controllerTwo.IsConnected)
                            instance.Controller.SelectedItem = "Two";
                        else if (controllerThree.IsConnected)
                            instance.Controller.SelectedItem = "Three";
                        else if (controllerFour.IsConnected)
                            instance.Controller.SelectedItem = "Four";
                    }
                });
            }
        }

        private void PollSerial()
        {
            Boolean updated = false;
            List<String> tempList = new List<String>();
            foreach (string s in SerialPort.GetPortNames())
            {
                tempList.Add(s);
            }

            foreach (var item in tempList)
            {
                if (!serialList.Contains(item))
                {
                    serialList.Add(item);
                    updated = true;
                }
            }
            for (int i = serialList.Count - 1; i >= 0; i--)
            {
                var item = serialList.ElementAt(i);
                if (!tempList.Contains(item))
                {
                    serialList.Remove(item);
                    updated = true;
                }
            }
            if (updated)
            {
                this.Dispatcher.Invoke(() =>
                {
                    instance.Serial.Items.Refresh();
                    if (serialList.Count > 0)
                    {
                        if (serialList.Contains(serialName))
                        {
                            instance.Serial.SelectedItem = serialName;
                        }
                    }
                });
            }
        }

        public static void SaveSettings()
        {
            Console.WriteLine("Saving settings file...");
            string file = @"settings.dat";
            File.WriteAllText(file, deadzoneRadius.ToString() + "\n" + serialName + "\n" + updateDelay + "\n" + baudRate);
        }

        public static void ReadSettings()
        {
            Console.WriteLine("Reading settings file...");
            string file = @"settings.dat";
            if (File.Exists(file))
            {
                string[] settings = File.ReadAllLines(file, Encoding.UTF8);
                if (settings != null && settings.Length > 0)
                {
                    if (settings.Length >= 1)
                        deadzoneRadius = short.Parse(settings[0]);
                    if (settings.Length >= 2)
                        serialName = settings[1];
                    if (settings.Length >= 3)
                        updateDelay = short.Parse(settings[2]);
                    if (settings.Length >= 4)
                        baudRate = int.Parse(settings[3]);
                }
            }
        }
    }
}
