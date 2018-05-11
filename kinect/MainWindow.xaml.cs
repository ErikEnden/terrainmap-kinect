//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace kinect
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;

        /// <summary>
        /// Description of the data contained in the depth frame
        /// </summary>
        private FrameDescription depthFrameDescription = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap depthBitmap = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // create the bitmap to display
            List<Color> Colors = new List<Color>();
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 232, 11, 11));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 244, 125, 66));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 232, 144, 13));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 232, 177, 12));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 232, 224, 11));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 156, 224, 20));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 91, 224, 20));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 20, 224, 64));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 20, 224, 128));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 20, 224, 186));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 20, 169, 224));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 20, 122, 224));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 23, 20, 224));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 183, 20, 224));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 224, 20, 183));
            Colors.Add(System.Windows.Media.Color.FromArgb(255, 224, 20, 98));
            BitmapPalette PLT = new BitmapPalette(Colors);

            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Indexed8, PLT);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public List<Color> Colors { get; }
        public ImageSource ImageSource
        {
            get
            {
                return this.depthBitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }
        
        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            maxDepth = depthFrame.DepthMaxReliableDistance;

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;
            byte color = 0;
            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];
                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                if (depth > minDepth && depth < maxDepth)
                {
                    if (depth > 800 && depth <= 820)
                    {
                        this.depthPixels[i] = (byte)(0);
                    }
                    else if (depth > 820 && depth <= 840)
                    {
                        this.depthPixels[i] = (byte)(1);
                    }
                    else if (depth > 840 && depth <= 860)
                    {
                        this.depthPixels[i] = (byte)(2);
                    }
                    else if (depth > 860 && depth <= 880)
                    {
                        this.depthPixels[i] = (byte)(3);
                    }
                    else if (depth > 880 && depth <= 900)
                    {
                        this.depthPixels[i] = (byte)(4);
                    }
                    else if (depth > 900 && depth <= 920)
                    {
                        this.depthPixels[i] = (byte)(5);
                    }
                    else if (depth > 920 && depth <= 940)
                    {
                        this.depthPixels[i] = (byte)(6);
                    }
                    else if (depth > 940 && depth <= 960)
                    {
                        this.depthPixels[i] = (byte)(7);
                    }
                    else if (depth > 960 && depth <= 980)
                    {
                        this.depthPixels[i] = (byte)(8);
                    }
                    else if (depth > 980 && depth <= 1000)
                    {
                        this.depthPixels[i] = (byte)(9);
                    }
                    else if (depth > 1000 && depth <= 1020)
                    {
                        this.depthPixels[i] = (byte)(10);
                    }
                    else if (depth > 1020 && depth <= 1040)
                    {
                        this.depthPixels[i] = (byte)(11);
                    }
                    else if (depth > 1040 && depth <= 1060)
                    {
                        this.depthPixels[i] = (byte)(12);
                    }
                    else if (depth > 1060 && depth <= 1080)
                    {
                        this.depthPixels[i] = (byte)(13);
                    }
                    else if (depth > 1080 && depth <= 1100)
                    {
                        this.depthPixels[i] = (byte)(14);
                    }
                    else
                    {
                        this.depthPixels[i] = (byte)(1);
                    }
                }
                else if (depth > maxDepth)
                {
                    this.depthPixels[i] = (byte)(6);
                }
            }
               
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
    }
}
