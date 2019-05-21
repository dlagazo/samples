// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.System;
using Windows.Storage;
using System.Collections.Generic;
namespace SerialSample
{    
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;
        byte cmdByte = 0x53;
        byte df = 0x08;
        byte checksum = 0x5B;
        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        private ObservableCollection<ChartData> _osData = new ObservableCollection<ChartData>()
        {
            new ChartData() { pr=88, spo2=96 },
            new ChartData() { pr=88, spo2=96 },
            new ChartData() { pr=88, spo2=96 },
            new ChartData() { pr=88, spo2=96 },
            new ChartData() { pr=88, spo2=96 },
        };

        public ObservableCollection<ChartData> chartData { get { return _osData;}}

        public class ChartData
        {
            public double spo2 { get; set; }
            public double pr { get; set; }
        }

        DispatcherTimer dispatcherTimer;
        bool isConnected = false;

        public MainPage()
        {
            
            this.InitializeComponent();            
            comPortInput.IsEnabled = true;
            listOfDevices = new ObservableCollection<DeviceInformation>();

            var uri = new Uri("ms-appx-web:///chart.html");
            webView.Source = uri;
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0,0,0,0,500);
            dispatcherTimer.Start();
        }

       
       

        // callback runs on UI thread
        async void dispatcherTimer_Tick(object sender, object e)
        {
            string aqs = SerialDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(aqs);

            if(dis.Count == 0)
            {
                status.Text = "STATUS: DISCONNECTED";
                isConnected = false;
            }

           
            if (!isConnected)
            {
                ListAvailablePorts();
                var selection = ConnectDevices.Items;//.SelectedItems;

                if (selection.Count <= 0)
                {
                    status.Text = "Select a device and connect";
                    isConnected = false;
                    comPortInput.Content = "START";
                    statusTxt.Text = "STATUS: DISCONNECTED";

                    return;
                }

                DeviceInformation entry = (DeviceInformation)selection[0];

                if (comPortInput.Content.ToString() == "RESET")
                {
                    sendText.Text = "";
                    CloseDevice();
                }

                try
                {
                    serialPort = await SerialDevice.FromIdAsync(entry.Id);

                    // Disable the 'Connect' button 

                    comPortInput.Content = "RESET";

                    // Configure serial settings
                    serialPort.WriteTimeout = TimeSpan.FromMilliseconds(0);
                    serialPort.ReadTimeout = TimeSpan.FromMilliseconds(0);
                    serialPort.BaudRate = 9600;
                    serialPort.Parity = SerialParity.None;
                    serialPort.StopBits = SerialStopBitCount.One;
                    serialPort.DataBits = 8;
                    serialPort.Handshake = SerialHandshake.None;

                    // Display configured settings
                    status.Text = "Serial port configured successfully: ";
                    status.Text += serialPort.BaudRate + "-";
                    status.Text += serialPort.DataBits + "-";
                    status.Text += serialPort.Parity.ToString() + "-";
                    status.Text += serialPort.StopBits;

                    // Set the RcvdText field to invoke the TextChanged callback
                    // The callback launches an async Read task to wait for data
                    rcvdText.Text = "Waiting for data...";

                    // Create cancellation token object to close I/O operations when closing the device
                    ReadCancellationTokenSource = new CancellationTokenSource();

                    // Enable 'WRITE' button to allow sending data
                    //status.Text = "Connected:" + entry.Id.ToString();
                    Listen();
                    isConnected = true;
                }
                catch (Exception ex)
                {
                    status.Text = ex.Message;
                    comPortInput.IsEnabled = true;
                }
            }

           
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);
                
                status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    if(dis[i].Id.Contains("FTDIBUS"))
                        listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                comPortInput.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void comPortInput_Click(object sender, RoutedEventArgs e)
        {
            //await webView.InvokeScriptAsync("eval", new string[] { "test(1,2,3')" });

            ListAvailablePorts();
            var selection = ConnectDevices.Items;//.SelectedItems;

            if (selection.Count <= 0)
            {
                status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            if(comPortInput.Content.ToString() == "RESET")
            {
                sendText.Text = "";
                CloseDevice();
            }
            

            try
            {                
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Disable the 'Connect' button 
                
                comPortInput.Content = "RESET";

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(0);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(0);                
                serialPort.BaudRate = 9600;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                status.Text = "Serial port configured successfully: ";
                status.Text += serialPort.BaudRate + "-";
                status.Text += serialPort.DataBits + "-";
                status.Text += serialPort.Parity.ToString() + "-";
                status.Text += serialPort.StopBits;

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                rcvdText.Text = "Waiting for data...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                // Enable 'WRITE' button to allow sending data
                //status.Text = "Connected:" + entry.Id.ToString();
                Listen();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                comPortInput.IsEnabled = true;
            }
        }

        /// <summary>
        /// sendTextButton_Click: Action to take when 'WRITE' button is clicked
        /// - Create a DataWriter object with the OutputStream of the SerialDevice
        /// - Create an async task that performs the write operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void sendTextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {                
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";                
                }
            }
            catch (Exception ex)
            {
                status.Text = "sendTextButton_Click: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync()
        {
            Task<UInt32> storeAsyncTask;

            if (sendText.Text.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(sendText.Text);                

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {                    
                    status.Text = sendText.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
                sendText.Text = "";
            }
            else
            {
                //status.Text = "Enter the text you want to write and then click on 'WRITE'";
            }
        }

        private async Task WriteCmd()
        {
            Task<UInt32> storeAsyncTask;

            try
            {
                // Load the text from the sendText input text box to the dataWriter object
                Byte[] cmd = new byte[3];
                cmd[0] = cmdByte;
                cmd[1] = df;
                cmd[2] = checksum;
                dataWriteObject.WriteBytes(cmd);//.WriteString(cmdByte.ToString() + df.ToString() + checksum.ToString());
                
                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = "Command:" + System.Text.Encoding.UTF8.GetString(cmd);
                }
               
            }
            catch(Exception e)
            {
                status.Text = "Unable to write command";
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {

                    //dataWriteObject = new DataWriter(serialPort.OutputStream);
                    dataReaderObject = new DataReader(serialPort.InputStream);
                    //await WriteCmd();

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    status.Text = "Reading task was cancelled, closing device and cleaning up";
                    CloseDevice();
                }
                else
                {
                    status.Text = ex.Message;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }
        Byte[] readGlobal = new Byte[75 * 5];

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        List<string> lines = new List<string>();
        int count = 0;
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
           
            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;
            sendText.Text = "";
            if (bytesRead > 0)
            {
                statusTxt.Text = "STATUS: CONNECTED";

                //dataReader.ReadBytes(stream);
                //Convert.ToBase64String(stream);
                //var read = dataReaderObject.(bytesRead);
                Byte[] read = new Byte[75*5*2];
                readGlobal = read;
                dataReaderObject.ReadBytes(read);
                //rcvdText.Text = read;
                //            rcvdText.Text = dataReaderObject.ReadString(bytesRead);
                //byte[] read = new byte[1024];
                //dataReaderObject.ReadBytes(read);
                rcvdText.Text = System.Text.Encoding.UTF8.GetString(read);
                //status.Text = "bytes read successfully!";
                var str = System.Text.Encoding.UTF8.GetString(read);
                //if(Byte.Parse(str[0])
                int ePrdMsb = 0, ePrdLsb = 0;
                double pleth = 0, spo2 = 0, pr = 0, spo2bb = 0;
                if (GetBit(read[0], 0))
                {
                    for (int j = 0; j < 75; j++)
                    {
                        if(true)//(!GetBit(read[(j * 5)], 4) && !GetBit(read[(j * 5)], 3))
                        {
                            var line = "S:" + GetBit(read[(j * 5)], 0) + ", F" + (j + 1) + ", B1:" + read[(j * 5)]
                            + ", B2:" + read[(j * 5) + 1] + ", B3:" + read[(j * 5) + 2] + ", B4:" + read[(j * 5) + 3] + ", B5:" + read[(j * 5) + 4]
                            + ", OOT:" + GetBit(read[(j * 5)], 4) + ", SNSA:" + GetBit(read[(j * 5)], 3)
                            + ", PLETH:" + ((read[(j * 5) + 1] * 256) + read[(j * 5) + 2]) + getValue(j + 1, read[(j * 5) + 3]);
                            //105 + 102 = 134
                            //" RPRF:" + GetBit(read[(j * 5)], 2)
                            //+ "\r\n";
                                if ((j + 1) == 22)
                                    ePrdMsb = read[(j * 5) + 3] * 256;
                                else if ((j + 1) == 23)
                                    ePrdLsb = read[(j * 5) + 3];
                            sendText.Text += line + "\r\n";

                            if (j + 1 == 3 || j+1 == 28 || j+1 == 53)
                            {
                                //pleth = ((read[(j * 5) + 1] * 256) + read[(j * 5) + 2]) / 65535 * 100;
                                spo2 = read[(j * 5) + 3];

                            }
                            if (j + 1 == 11 || j + 1 == 36 || j + 1 == 61)
                            {
                                //pleth = ((read[(j * 5) + 1] * 256) + read[(j * 5) + 2]) / 65535 * 100;
                                spo2bb = read[(j * 5) + 3];

                            }
                            if (j+1 == 20 || j+1 == 45 || j+1 == 70)
                            {
                                pr = Convert.ToInt32(GetBit(read[((j) * 5) + 3], 1)) * 256 + Convert.ToInt32(GetBit(read[((j) * 5) + 3], 0)) * 128 + read[((j+1) * 5) + 3];
                                lines.Add(count +"," + spo2 + "," + pr + "," + spo2bb );
                                count++;
                                await webView.InvokeScriptAsync("eval", new string[] { "test('" + count + "'," + spo2 + ", " + pr + ", " + spo2bb + ")" });
                                //chartData.Add(new ChartData { spo2 = spo2, pr = pr });
                            }

                            //lines.Add(line);
                            //await Windows.Storage.FileIO.WriteTextAsync(file, line);
                        }
                            
                    }
                }
                else
                {
                    CloseDevice();
                    sendText.Text += "BAD DATA: PLEASE RETRY CONNECTION";
                }
             
                Debug.WriteLine("E-PR-D:" + ((ePrdMsb + ePrdLsb) * 282/65535) + 18);
                
                

            }
           
        }

        private string getValue(int j, byte val)
        {
            if (j == 3 || j == 28 || j == 53)
                return ", SPO2:" + val;
            else if (j == 20 || j == 45 || j == 70)
                return ", PR:" + Convert.ToInt32(GetBit(readGlobal[((j-1) * 5) + 3],1))*256 + Convert.ToInt32(GetBit(readGlobal[((j - 1) * 5) + 3], 0)) * 128 + readGlobal[((j) * 5) + 3];




            return ",";
        }



        private string GetSPO2(byte spo2)
        {
            if (spo2 < 127)
                return (spo2 / 127 * 100).ToString();

            else
                return "";
                
        }

        
        private bool GetBit( byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {         
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }         
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            CancelReadTask();
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

            comPortInput.IsEnabled = true;
            rcvdText.Text = "";
            listOfDevices.Clear();               
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }          
        }
        Windows.Storage.StorageFile file;
        private async void saveFile_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            string dt = DateTime.Now.Date.Year + "_" + DateTime.Now.Date.Month + "_" + DateTime.Now.Date.Day + "_" + DateTime.Now.TimeOfDay.Ticks;
            Windows.Storage.StorageFile sampleFile =
                await storageFolder.CreateFileAsync(txtFileName.Text + dt + ".csv",
                    Windows.Storage.CreationCollisionOption.ReplaceExisting);
          
            file =
                await storageFolder.GetFileAsync(txtFileName.Text + dt + ".csv");
        }

        private async void openFolder_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(ApplicationData.Current.LocalFolder.Path));
        }

        private async void saveRecord_Click(object sender, RoutedEventArgs e)
        {
            await Windows.Storage.FileIO.WriteLinesAsync(file, lines);

        }
        int loop = 0;
        private async void addData_Click(object sender, RoutedEventArgs e)
        {
            loop++;
            Random rand = new Random(DateTime.Now.Millisecond);
            
            await webView.InvokeScriptAsync("eval", new string[] { "test('" + loop + "',96, " + rand.Next() % 100 + ")" });

        }
    }
}
