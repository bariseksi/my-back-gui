using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Threading;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;

namespace MyBack
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GattCharacteristic angleCharacteristic;
        GattCharacteristic batteryCharacteristic;
        static Guid battery_Guid = Guid.Parse("28faaba7-417a-413c-a827-b247e68c12df");
        static Guid angle_Guid = Guid.Parse("918f19a4-c5c1-4194-8fa4-d78a4eb9db94");

        Color iconColor = Colors.Black;
        int ang = 0;
        float volt = 0;
        int batteryPercentage = 0;
        int warningLevel = 70;
        int updateInterval = 1000; //milliseconds

        int[] warningLevels = new int[] { 60, 65, 70, 75, 80, 85 };
        int warningLevelIndex = 0;
        readonly string warningLevelString = "Warning Level : ";
        int warningLevelSecond = 0;
        int warningLevelTimeout = 10; //10 Seconds
        bool inOperationScreen = false;
        DeviceWatcher deviceWatcher;
        ulong deviceID = Convert.ToUInt64("c8c9a3d1ac32", 16);

        Brush foregroud; 

        public MainWindow()
        {

            InitializeComponent();
            SwitchScreen(false);
            warningLevel = warningLevels[warningLevelIndex];
            foregroud = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
            SearchBLE();
        }

        private void SearchBLE()
        {


            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress" };

            deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                ""/*BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(deviceID, BluetoothAddressType.Public)*/,
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            //deviceWatcher.Updated += DeviceWatcher_Updated;
            //deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            //deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            //deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            
            deviceWatcher.Start();

        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            
            App.Current.Dispatcher.Invoke(() => 
            {
                label_Voltage.Content = $"{args.Name}";
                if (args.Name == "dd-ble")
                {
                    deviceWatcher.Stop();

                    label_WarningLevel.Content = $"Connecting";
                    label_Voltage.Content = $"Found {args.Name}";
                    InitiateBLEConnection(args.Id);
                }
            });

            
        }

        private async void InitiateBLEConnection(string id)
        {
 
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(id);

            GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesForUuidAsync(Guid.Parse("e653fc6d-d701-4a73-ab2c-794482caaba6"));

            if (result.Status == GattCommunicationStatus.Success)
            {
                var service = result.Services.FirstOrDefault();
                if (service != null)
                {
                    GattCharacteristicsResult characteristicResult = await service.GetCharacteristicsAsync();

                    if (characteristicResult.Status == GattCommunicationStatus.Success)
                    {
                        batteryCharacteristic = characteristicResult.Characteristics.FirstOrDefault(X => X.Uuid == battery_Guid);
                        angleCharacteristic = characteristicResult.Characteristics.FirstOrDefault(X => X.Uuid == angle_Guid);

                        if(batteryCharacteristic != null && angleCharacteristic != null)
                        {
                            //isConnected = true;
                            //label_Status.Content = "Connected";
                            GattCommunicationStatus status = await batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            if (status == GattCommunicationStatus.Success)
                                batteryCharacteristic.ValueChanged += Characteristic_ValueChanged;

                            status = await angleCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            if (status == GattCommunicationStatus.Success)
                                angleCharacteristic.ValueChanged += Characteristic_ValueChanged;

                            SwitchScreen(true);
                        }
                    }

                    
                }
            }
            else
            {

                var r = MessageBox.Show("Unable to connect\nTry again?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes)
                    SearchBLE();
                else
                    Environment.Exit(0);
            }
        }

        
        private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            reader.ByteOrder = ByteOrder.LittleEndian;

            if (sender.Uuid == battery_Guid)
                volt = reader.ReadInt16() / 100.0f;
            else if (sender.Uuid == angle_Guid)
                ang = reader.ReadInt16();

            DataReceived(ang, volt);
        }

        private async void ReceiveData(object state)
        {
            int ang = 0;
            float volt = 0;

            var res = await angleCharacteristic.ReadValueAsync();
            if(res.Status == GattCommunicationStatus.Success)
            {
                var reader = DataReader.FromBuffer(res.Value);
                reader.ByteOrder = ByteOrder.LittleEndian;
                ang = reader.ReadInt16();
            }

            res = await batteryCharacteristic.ReadValueAsync();
            if (res.Status == GattCommunicationStatus.Success)
            {
                var reader = DataReader.FromBuffer(res.Value);
                reader.ByteOrder = ByteOrder.LittleEndian;
                volt = reader.ReadInt16() / 100.0f;
            }

            if(ang >0 && volt > 0)
            {
                DataReceived(ang, volt);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public ImageSource CreateTextIcon(string str)
        {

            PixelFormat pf = PixelFormats.Pbgra32;
            int width = 5;
            int height = 5;
            int rawStride = (width * pf.BitsPerPixel + 7) / 8;
            byte[] rawImage = new byte[rawStride * height];

            BitmapSource src = BitmapSource.Create(width, height, 96, 96, pf, null, rawImage, rawStride);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                var text = new FormattedText(str, new System.Globalization.CultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("fixedsys"), 35, new SolidColorBrush(iconColor));
                dc.DrawImage(src, new Rect(0, 0, width, height));
                dc.DrawText(text, new Point(0, 0));
            }
            return new DrawingImage(dv.Drawing);
        }

        private void DataReceived(int floatAngle, float voltage)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (floatAngle <= warningLevel)
                {
                    label_Angle.Foreground = new SolidColorBrush(Colors.DarkRed);
                    warningLevelSecond++;
                }
                else
                {
                    label_Angle.Foreground = foregroud;
                    warningLevelSecond = 0;
                    this.Topmost = false;
                }
                label_Angle.Content = $"{floatAngle}°";


                label_BatteryPercentage.Foreground = foregroud;
                batteryPercentage = (int)((125 * voltage) - 425);
                if (batteryPercentage < 0)
                    batteryPercentage = 0;
                else if (batteryPercentage <= 20)
                    label_BatteryPercentage.Foreground = Brushes.DarkRed;
                else if (batteryPercentage > 100)
                    batteryPercentage = 100;

                label_Voltage.Content = $"{voltage}v";
                label_BatteryPercentage.Content = $"{batteryPercentage}%";


                if (warningLevelSecond / (updateInterval / 1000) > warningLevelTimeout)
                {
                    this.Topmost = true;
                    this.WindowState = WindowState.Normal;
                }
                this.Icon = CreateTextIcon(floatAngle.ToString());

            }));
        }

        private void SwitchScreen(bool toOperationScreen)
        {
            if(toOperationScreen)
            {
                label_Angle.Visibility = Visibility.Visible;
                label_Voltage.Visibility = Visibility.Hidden;
                label_BatteryPercentage.Visibility = Visibility.Visible;
                label_WarningLevel.Visibility = Visibility.Visible;
              
                label_WarningLevel.Content = $"{warningLevelString}{warningLevel}";
                inOperationScreen = true;
            }
            else
            {
                label_Angle.Visibility = Visibility.Collapsed;
                label_Voltage.Visibility = Visibility.Visible;
                label_BatteryPercentage.Visibility = Visibility.Collapsed;
                label_WarningLevel.Visibility = Visibility.Visible;
                label_Voltage.Content = "...";

                label_WarningLevel.Content = $"Searching";
                inOperationScreen = false;
            }
        }

        private void Label_WarningLevel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(!inOperationScreen)
            {
                Environment.Exit(1);
                return;
            }

            warningLevelIndex++;
            if (warningLevelIndex >= warningLevels.Length)
                warningLevelIndex = 0;
            warningLevel = warningLevels[warningLevelIndex];
            label_WarningLevel.Content = $"{warningLevelString}{warningLevel}";
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            if (mousePosition.Y > 20)
                this.WindowState = WindowState.Minimized;
        }

        private void Label_Voltage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            label_Voltage.Visibility = Visibility.Hidden;
            label_BatteryPercentage.Visibility = Visibility.Visible;
        }

        private void Label_BatteryPercentage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            label_Voltage.Visibility = Visibility.Visible;
            label_BatteryPercentage.Visibility = Visibility.Hidden;
        }
    }
}
