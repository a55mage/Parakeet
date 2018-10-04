using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Diagnostics;
using Windows.UI.Core;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Windows.Networking;
using Windows.Networking.Sockets;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.ComponentModel;
using System.Data;


namespace Parakeet2
{
    public sealed partial class MainPage : Page
    {
        Guid HRserviceGuid = new Guid("0000180d-0000-1000-8000-00805f9b34fb"); //Heart rate service's GUID
        Guid HRMeasurement = new Guid("00002A37-0000-1000-8000-00805F9B34FB"); //Heart rate monitor characteristic's GUID
        GattCharacteristic HRreader = null; //reader structure for the heart rate data
        BluetoothLEDevice BluetoothDevice; //data variable for the chosen device
        DeviceInformationCollection devices; //list of devices already paired with the pc
        public static string BPMstring = ""; //string for the heart rate value

        bool deviceConnectionState = false;
        bool clientConnectionState = false;
        Stream inStream; //TCP server input stream
        StreamReader reader; //reader for input stream

        bool connected = false;
        DispatcherTimer dispatcherTimer; //timer for pinging the BLE device
        string serverPort = "13000"; //server port
        StreamSocket socket = new StreamSocket(); //TCP client socket to connect to the server
        HostName serverHost = new HostName("localhost");


        public MainPage()//visualize the main form 
        {
            InitializeComponent();
            //call the function PairedDevices as async, otherwise everything freezes
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    PairedDevices();
                }
            );
        }

        //Add a device to the listview, this function is called inside of PairedDevices()
        async void DisplayDeviceInterface(DeviceInformation deviceInterface)
        {
            var name = deviceInterface.Name;
            DeviceInterfacesOutputLst.Items.Insert(0, name);
            OutputList.Items.Insert(0, name + " added");
        }

        //Finds the devices already paired with the pc and add them to the listview
        private async void PairedDevices()
        {
            DeviceInterfacesOutputLst.Items.Clear();
            devices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());

            foreach (var device in devices)
            {
                DisplayDeviceInterface(device);
            }

            OutputList.Items.Insert(0, "Showing all paired BLE devices");
            OutputList.Items.Insert(0, "Select you device and click ''Connect''");
        }

        //Connect to the chosen device
        private async void ConnectDevice()
        {
            if (DeviceInterfacesOutputLst.SelectedItem != null)
            {
                string DeviceID = ""; //Device ID
                foreach (var device in devices.Where(device => device.Name == DeviceInterfacesOutputLst.SelectedItem.ToString())) //selext the chosen device from the listview
                    DeviceID = device.Id;
                BluetoothDevice = await BluetoothLEDevice.FromIdAsync(DeviceID); //request access to the Device
                DeviceAccessStatus x = await BluetoothDevice.RequestAccessAsync(); //wait for the permission
                OutputList.Items.Insert(0, "Connection: " + x.ToString() + " - BluetoothLE Device is " + BluetoothDevice.Name);
                //Create the service and characteristic values to fill with the chosen device information
                GattDeviceService HRservice = null;
                GattCharacteristicsResult HRcharacteristics = null;

                try //read the device characteristics, if the characteristics are not found, an exception get thrown
                {
                    HRservice = BluetoothDevice.GetGattService(HRserviceGuid);
                    HRcharacteristics = await HRservice.GetCharacteristicsAsync();
                }
                catch
                {
                    OutputList.Items.Insert(0, "Chosen device does not support HR service, choose another one");
                    return;
                }
                //TFind the characteristics UUID and assign them to the variable
                foreach (GattCharacteristic caratteristica in HRcharacteristics.Characteristics.Where(caratteristica => caratteristica.Uuid.Equals(HRMeasurement)))
                {
                    HRreader = caratteristica; //assegno la caratteristica ad HRreader
                    OutputList.Items.Insert(0, "Heart Rate Monitor characteristic found - Handle: " + HRreader.AttributeHandle.ToString());
                }

                //check the server port data
                try
                {
                    int serverPortInt;
                    serverPortInt = Int32.Parse(tcpPortText.Text);
                    serverPort = tcpPortText.Text;

                }
                catch
                {
                    OutputList.Items.Insert(0, "Invalid TCP Port, using 13000");
                    tcpPortText.Text = "13000";
                    serverPort = tcpPortText.Text;

                }

                if (HRreader == null)//if the HR characteristic in not found, show an error
                    OutputList.Items.Insert(0, "Heart Rate Monitor characteristic not found");
                else //If the characteristic have been found, start the readings
                {
                    //Requesting notify
                    //NOTE: we are not allowed to read the value on request, we have to ask the device to be notified when the HR value change
                    GattCommunicationStatus status = GattCommunicationStatus.ProtocolError; //setting the status as "protocol error", just in case...
                    OutputList.Items.Insert(0, "Waiting for notify handshake...");

                    try
                    {
                        status = await HRreader.WriteClientCharacteristicConfigurationDescriptorAsync(
                                                GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    }
                    catch
                    {
                        //fuck, i don't know
                    }

                    //We are now ready to receive the informations and send them via TCP
                    if (status == GattCommunicationStatus.Success)
                    {
                        OutputList.Items.Insert(0, "Notify Activated");
                        OutputList.Items.Insert(0, "Now reading... give me a few seconds");
                        deviceConnectionState = true;
                        serverPort = tcpPortText.Text;
                        OutputList.Items.Insert(0, "Connecting to port " + serverPort);

                        DispatcherTimerSetup();
                        read();
                    }
                    else
                    {
                        if (status == GattCommunicationStatus.ProtocolError)
                            OutputList.Items.Insert(0, "Notify Failed - Protocol Error");
                        if (status == GattCommunicationStatus.Unreachable)
                            OutputList.Items.Insert(0, "Notify Failed - Unreachable");
                        if (status == GattCommunicationStatus.AccessDenied)
                            OutputList.Items.Insert(0, "Notify Failed - Access Denied");
                        OutputList.Items.Insert(0, "Sorry, I'm far from perfect");
                    }
                }
            }
            else
            {
                OutputList.Items.Insert(0, "Select a device from the list"); //nel caso in cui non venga selezionato un dispositivo dalla lista
            }
        }


       
        //connect the client to the server TCP
        async void ConnectToServer(HostName serverHost, string serverPort)
        {
            if (connected == false)
            {
                try
                {
                    await socket.ConnectAsync(serverHost, serverPort);
                    connected = true;
                }
                catch
                {
                    Debug.WriteLine("socket.ConnectAsync exception");
                    connected = false;
                }
            }
        }

        //send informations to the TCP server
        async void ClientSend(string message)
        {
            try
            {
                Debug.WriteLine("invio il messaggio");
                Stream streamOut = socket.OutputStream.AsStreamForWrite();
                StreamWriter writer = new StreamWriter(streamOut);
                await writer.WriteLineAsync(message);
                await writer.FlushAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace);
            }
            finally
            {

            }
        }

        //create the event handler to check when the HR value changes
        private async void read()
        {
            //this characteristic is never readable, is only notificable, read the GATT manual for more bullshit about this
            HRreader.ValueChanged += valueChanged;
        }

        //event catcher
        async void valueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            int arrayLenght = (int)args.CharacteristicValue.Length;
            byte[] hrData = new byte[arrayLenght];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(hrData);
            var hrValue = ProcessData(hrData); //process the data to make them more human friendly
            BPMstring = hrValue.ToString();
            //write the value in the heart shaped container
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    write();
                }
            );
        }

        private void ShowHeartRed() //show the red heart and hide the white one
        {
            HeartWhite.Visibility = Visibility.Collapsed;
            HeartRed.Visibility = Visibility.Visible;
        }

        private void ShowHeartWhite() //show the white heart and hide the red one
        {
            HeartWhite.Visibility = Visibility.Visible;
            HeartRed.Visibility = Visibility.Collapsed;
        }

        //translate the data coming from the device
        private int ProcessData(byte[] data)
        {
            // Heart Rate profile defined flag values
            const byte heartRateValueFormat = 0x01;

            byte currentOffset = 0;
            byte flags = data[currentOffset];
            bool isHeartRateValueSizeLong = ((flags & heartRateValueFormat) != 0); //true if 16 bit, false if  8 bit

            currentOffset++;

            ushort heartRateMeasurementValue;

            if (isHeartRateValueSizeLong)
            { //16 bit
                heartRateMeasurementValue = (ushort)((data[currentOffset + 1] << 8) + data[currentOffset]);
                currentOffset += 2;
            }
            else
            { //8 bit
                heartRateMeasurementValue = data[currentOffset];
            }
            return heartRateMeasurementValue;
        }

        //SCRIVE IL VALORE DELL'HR NELLA TEXTBOX
        private async void write()
        {
            OutputText.Text = BPMstring;

            if (HeartRed.Visibility == Visibility.Collapsed)
            {
                ShowHeartRed();
            }
        }

        //MOSTRA LE CARATTERISTICHE DEL SERVIZIO HR
        private async void servizi()
        {
            GattDeviceService HRservice = BluetoothDevice.GetGattService(HRserviceGuid);
            GattCharacteristicsResult HRcharacteristics = await HRservice.GetCharacteristicsAsync();
            foreach (GattCharacteristic caratteristica in HRcharacteristics.Characteristics.Where(caratteristica => caratteristica.AttributeHandle == 36)) //13
            {
                HRreader = caratteristica;
                OutputList.Items.Insert(0, "Servizio HR trovato in" + HRreader.CharacteristicProperties.ToString());
            }
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            ConnectDevice();
        }

        async private void DoPing() //ping the device to see if it's still reachable
        {
            GattCommunicationStatus status = GattCommunicationStatus.ProtocolError;
            status = await HRreader.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (status == GattCommunicationStatus.Success)
            {
                if (deviceConnectionState == false)
                {
                    deviceConnectionState = true;//the device is reachable again
                    OutputList.Items.Insert(0, "Device now Reachable");
                }
            }
            else // 
            {
                if (deviceConnectionState == true)
                {
                    deviceConnectionState = false; //the device is not reachable
                    OutputList.Items.Insert(0, "Device Problem, is it unreachable?");
                }
            }
        }


        private async void Reset()
        { //reset the app, but sometimes have problem with the TCP client, don't have time to fix it
            try
            {
                await HRreader.WriteClientCharacteristicConfigurationDescriptorAsync(
                                                GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
                Debug.WriteLine("something happened, better close the app");
            }

            PairedDevices();
            OutputList.Items.Clear();
            deviceConnectionState = false;
            connected = false;
            HRreader = null;
            OutputText.Text = "--";
            if (dispatcherTimer != null)
            {
                dispatcherTimer.Stop();
                dispatcherTimer = null;
            }
            inStream = null;
            reader = null;
            socket.Dispose();
            ShowHeartWhite();
        }


        public void DispatcherTimerSetup() //timer setup (suggested tick rate is 2 seconds)
        {
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 2);
            dispatcherTimer.Start();
        }

        void dispatcherTimer_Tick(object sender, object e)
        {
            DoPing();
            if (connected == false)
            {
                ConnectToServer(serverHost, serverPort);
            }
            //num++;
            ClientSend(BPMstring);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }
    }
}

