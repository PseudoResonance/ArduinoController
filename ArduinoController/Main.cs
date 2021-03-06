﻿using SharpDX;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Media;
using static ArduinoController.MainWindow;

namespace ArduinoController
{
    class Main
    {
        public static short deadzoneRadius = 3500;
        public static byte updateDelay = 20;
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
                    _isSerialReady = value;
                    MainWindow.instance.SerialStatus.Fill = new SolidColorBrush(value ? Color.FromRgb(0, 255, 0) : Color.FromRgb(255, 0, 0));
                });
            }
        }
        /*private static Boolean _isJavaConnected = false;
        private static Boolean IsJavaConnected
        {
            get { return _isJavaConnected; }
            set
            {
                MainWindow.instance.Dispatcher.Invoke(() =>
                {
                    _isJavaConnected = value;
                    MainWindow.instance.JavaStatus.Fill = new SolidColorBrush(value ? Color.FromRgb(0, 255, 0) : Color.FromRgb(255, 0, 0));
                });
            }
        }*/

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
            float leftX = 0, leftY = 0, rightX = 0, rightY = 0, leftTrigger = 0, rightTrigger = 0;
            GamepadButtonFlags buttons = 0;
            try
            {
                if (joystick != null)
                {
                    var state = joystick.GetState();
                    leftX = CalculateDeadzone(state.Gamepad.LeftThumbX);
                    leftY = CalculateDeadzone(state.Gamepad.LeftThumbY);
                    rightX = CalculateDeadzone(state.Gamepad.RightThumbX);
                    rightY = CalculateDeadzone(state.Gamepad.RightThumbY);
                    leftTrigger = CalculatePercentage(state.Gamepad.LeftTrigger);
                    rightTrigger = CalculatePercentage(state.Gamepad.RightTrigger);
                    buttons = state.Gamepad.Buttons;
                }
            }
            catch (SharpDXException) { }
            try
            {
                byte[] tempArray = BitConverter.GetBytes(leftX);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 2, 4);
                tempArray = BitConverter.GetBytes(leftY);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 6, 4);
                tempArray = BitConverter.GetBytes(rightX);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 10, 4);
                tempArray = BitConverter.GetBytes(rightY);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 14, 4);
                tempArray = BitConverter.GetBytes(leftTrigger);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 18, 4);
                tempArray = BitConverter.GetBytes(rightTrigger);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 22, 4);
                tempArray = BitConverter.GetBytes((ushort)buttons);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(tempArray);
                Array.Copy(tempArray, 0, buffer, 26, 2);
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
                if (serialPort != null && serialPort.IsOpen && IsSerialReady)
                {

                    serialPort.Write(buffer, 0, buffer.Length);
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

        private static byte[] ConvertToArray(float input)
        {
            byte[] ret = BitConverter.GetBytes(input);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(ret);
            return ret;
        }

        public static void CloseSerial(Object state)
        {
            ClosePort();
        }

        private static void ClosePort()
        {
            try
            {
                Console.WriteLine("Closing current port");
                if (serialPort.IsOpen)
                {
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                    serialPort.Close();
                }
                IsSerialReady = false;
                serialPort = null;
            }
            catch (IOException)
            {
                Console.WriteLine("Error when current port");
            }
        }

        public static void InitializeSerial(Object state)
        {
            if (MainWindow.testPortThread == null || !MainWindow.testPortThread.IsAlive)
            {
                SetupPort();
            }
        }

        private static bool SetupPort()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                ClosePort();
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
                    serialPort.StopBits = StopBits.One;
                    serialPort.Parity = Parity.None;
                    serialPort.DtrEnable = true;
                    serialPort.RtsEnable = true;
                    serialPort.Open();
                    IsSerialReady = true;
                    Console.WriteLine("Port open!");
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException)
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
                if (joystickName.Equals("One", StringComparison.InvariantCulture))
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
                if (joystickName.Equals("Two", StringComparison.InvariantCulture))
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
                if (joystickName.Equals("Three", StringComparison.InvariantCulture))
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
                if (joystickName.Equals("Four", StringComparison.InvariantCulture))
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
                    if (joystickName.Length == 0)
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

        public static void PollSerial(Object state)
        {
            PollSerial();
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
                            if (n.StartsWith(serialName, StringComparison.InvariantCulture))
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
            File.WriteAllText(file, deadzoneRadius + "\n" + serialName + "\n" + updateDelay + "\n" + baudRate + "\n" + MainWindow.currentTheme.ToString() + "\n" + JavaReceiver.ip + "\n" + JavaReceiver.port);
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
                        deadzoneRadius = short.TryParse(settings[0], out short output) ? output : (short) 3500;
                    if (settings.Length >= 2)
                        serialName = settings[1];
                    if (settings.Length >= 3)
                        updateDelay = byte.TryParse(settings[2], out byte output) ? output : (byte) 20;
                    if (settings.Length >= 4)
                    {
                        baudRate = int.TryParse(settings[3], out int output) ? output : 115200;
                        MainWindow.instance.BaudRate.Text = baudRate.ToString("D", CultureInfo.InvariantCulture);
                    }
                    if (settings.Length >= 5)
                    {
                        if (Enum.TryParse(settings[4], out WindowsTheme theme))
                            MainWindow.instance.SetTheme(theme);
                    }
                    /*if (settings.Length >= 6)
                    {
                        JavaReceiver.ip = settings[5];
                        MainWindow.instance.JavaIP.Text = settings[5];
                    }
                    if (settings.Length >= 7)
                    {
                        JavaReceiver.port = int.TryParse(settings[6], out int output) ? output : 2400;
                        MainWindow.instance.JavaPort.Text = JavaReceiver.port.ToString("D", CultureInfo.InvariantCulture);
                    }*/
                }
            }
        }
    }
}
