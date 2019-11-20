using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Media;

namespace ArduinoController
{
    class Main
    {
        public static short deadzoneRadius = 3500;
        public static short updateDelay = 20;
        public static int baudRate = 115200;

        public static String joystickName = "";
        public static Controller joystick = null;

        public static String serialName = "";
        public static SerialPort serialPort = null;
        private static Boolean _isSerialReady = false;
        private static Boolean IsSerialReady { get { return _isSerialReady; } set
            {
                MainWindow.instance.Dispatcher.Invoke(() =>
                {
                    MainWindow.instance.SerialStatus.Fill = new SolidColorBrush(value ? Color.FromRgb(0, 255, 0) : Color.FromRgb(255, 0, 0));
                });
            }
        }

        private static byte[] buffer = new byte[] { 67, 114, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static float CalculateDeadzone(short n)
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

        private static float CalculatePercentage(byte n)
        {
            float c = (float)n / byte.MaxValue;
            if (c > 1)
                c = 1;
            else if (c < -1)
                c = -1;
            return c;
        }

        public static void UpdateJoystick()
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
                if (serialPort != null && serialPort.IsOpen && IsSerialReady)
                {
                    byte[] tempArray = BitConverter.GetBytes(CalculateDeadzone(leftX));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 2, 4);
                    tempArray = BitConverter.GetBytes(CalculateDeadzone(leftY));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 6, 4);
                    tempArray = BitConverter.GetBytes(CalculateDeadzone(rightX));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 10, 4);
                    tempArray = BitConverter.GetBytes(CalculateDeadzone(rightY));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 14, 4);
                    tempArray = BitConverter.GetBytes(CalculatePercentage(leftTrigger));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 18, 4);
                    tempArray = BitConverter.GetBytes(CalculatePercentage(rightTrigger));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 22, 4);
                    tempArray = BitConverter.GetBytes((ushort)buttons);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(tempArray);
                    Array.Copy(tempArray, 0, buffer, 26, 2);

                    serialPort.Write(buffer, 0, buffer.Length);
                    if (MainWindow.showDebug)
                    {
                        String str = "";
                        foreach (byte b in buffer)
                        {
                            str += b + " ";
                        }
                        MainWindow.instance.Dispatcher.Invoke(() =>
                        {
                            MainWindow.instance.DebugOutput.Text = str;
                        });
                    }
                }
                else if (MainWindow.testPortThread == null)
                {
                    MainWindow.testPortThread = new Thread(() =>
                    {
                        TestPort();
                    });
                    MainWindow.testPortThread.Start();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException)
            {
                IsSerialReady = false;
                if (MainWindow.testPortThread == null)
                {
                    MainWindow.testPortThread = new Thread(() =>
                    {
                        TestPort();
                    });
                    MainWindow.testPortThread.Start();
                }
            }
        }

        public static void CloseSerial()
        {
            Console.WriteLine("Closing current port");
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            serialPort.Close();
            IsSerialReady = false;
        }

        public static bool SetupPort()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                CloseSerial();
            }
            if (serialName.Length > 0)
            {
                Console.WriteLine("Setting up Serial: " + serialName + " with baud rate: " + baudRate);
                try
                {
                    serialPort = new SerialPort(serialName);
                    serialPort.BaudRate = baudRate;
                    serialPort.ReadTimeout = 1000;
                    serialPort.WriteTimeout = 1000;
                    serialPort.Open();
                    IsSerialReady = true;
                    Console.WriteLine("Port open!");
                    return true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Port failed to open!");
                    IsSerialReady = false;
                    return false;
                }
            }
            return false;
        }

        public static void TestPort()
        {
            while (!MainWindow.shutdown && !IsSerialReady)
            {
                if (SetupPort())
                    break;
                Thread.Sleep(5000);
            }
            MainWindow.testPortThread = null;
        }

        public static void PollJoysticks()
        {
            Boolean updated = false;

            if (MainWindow.controllerOne.IsConnected)
            {
                if (!MainWindow.instance.joystickList.Contains("One"))
                {
                    MainWindow.instance.joystickList.Add("One");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("One"))
                    joystickName = "";
                if (MainWindow.instance.joystickList.Contains("One"))
                {
                    MainWindow.instance.joystickList.Remove("One");
                    updated = true;
                }
            }
            if (MainWindow.controllerTwo.IsConnected)
            {
                if (!MainWindow.instance.joystickList.Contains("Two"))
                {
                    MainWindow.instance.joystickList.Add("Two");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Two"))
                    joystickName = "";
                if (MainWindow.instance.joystickList.Contains("Two"))
                {
                    MainWindow.instance.joystickList.Remove("Two");
                    updated = true;
                }
            }
            if (MainWindow.controllerThree.IsConnected)
            {
                if (!MainWindow.instance.joystickList.Contains("Three"))
                {
                    MainWindow.instance.joystickList.Add("Three");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Three"))
                    joystickName = "";
                if (MainWindow.instance.joystickList.Contains("Three"))
                {
                    MainWindow.instance.joystickList.Remove("Three");
                    updated = true;
                }
            }
            if (MainWindow.controllerFour.IsConnected)
            {
                if (!MainWindow.instance.joystickList.Contains("Four"))
                {
                    MainWindow.instance.joystickList.Add("Four");
                    updated = true;
                }
            }
            else
            {
                if (joystickName.Equals("Four"))
                    joystickName = "";
                if (MainWindow.instance.joystickList.Contains("Four"))
                {
                    MainWindow.instance.joystickList.Remove("Four");
                    updated = true;
                }
            }
            if (updated)
            {
                MainWindow.instance.Dispatcher.Invoke(() =>
                {
                    MainWindow.instance.Controller.Items.Refresh();
                    if (joystickName.Equals(""))
                    {
                        if (MainWindow.controllerOne.IsConnected)
                            MainWindow.instance.Controller.SelectedItem = "One";
                        else if (MainWindow.controllerTwo.IsConnected)
                            MainWindow.instance.Controller.SelectedItem = "Two";
                        else if (MainWindow.controllerThree.IsConnected)
                            MainWindow.instance.Controller.SelectedItem = "Three";
                        else if (MainWindow.controllerFour.IsConnected)
                            MainWindow.instance.Controller.SelectedItem = "Four";
                    }
                });
            }
        }

        public static void PollSerial()
        {
            Boolean updated = false;
            List<String> tempList = new List<String>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());

                tempList = portnames.Select(n => n + " " + ports.FirstOrDefault(s => s.Contains(n))).ToList();
            }

            foreach (var item in tempList)
            {
                if (!MainWindow.instance.serialList.Contains(item))
                {
                    MainWindow.instance.serialList.Add(item);
                    updated = true;
                }
            }
            for (int i = MainWindow.instance.serialList.Count - 1; i >= 0; i--)
            {
                var item = MainWindow.instance.serialList.ElementAt(i);
                if (!tempList.Contains(item))
                {
                    MainWindow.instance.serialList.Remove(item);
                    updated = true;
                }
            }
            if (updated)
            {
                MainWindow.instance.Dispatcher.Invoke(() =>
                {
                    MainWindow.instance.Serial.Items.Refresh();
                    if (MainWindow.instance.serialList.Count > 0)
                    {
                        foreach (String n in MainWindow.instance.serialList)
                        {
                            if (n.StartsWith(serialName))
                            {
                                MainWindow.instance.Serial.SelectedItem = n;
                            }
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
                    {
                        baudRate = int.Parse(settings[3]);
                        MainWindow.instance.BaudRate.Text = baudRate.ToString();
                    }
                }
            }
        }
    }
}
