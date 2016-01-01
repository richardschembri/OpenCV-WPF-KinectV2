﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.Drawing;
using Microsoft.Kinect;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO;

namespace KinectWPFOpenCV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor;
        //WriteableBitmap depthBitmap;
        WriteableBitmap colorBitmap;
        //DepthImagePixel[] depthPixels;
        //richard
        MultiSourceFrameReader reader;
        byte[] colorPixels;

        int blobCount = 0;

        public MainWindow()
        {
            InitializeComponent();
           
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.MouseDown += MainWindow_MouseDown;

        }

      
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            //foreach (var potentialSensor in KinectSensor.KinectSensors)
            //{
            //    if (potentialSensor.Status == KinectStatus.Connected)
            //    {
            //        this.sensor = potentialSensor;
            //        break;
            //    }
            //}

            sensor = KinectSensor.GetDefault();
            
            if (null != this.sensor)
            {

                //this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                //this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                //this.colorPixels = new byte[this.sensor.ColorFrameSource.FrameDescription.LengthInPixels];
                var frameDesc = this.sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
                this.colorPixels = new byte[frameDesc.BytesPerPixel * frameDesc.LengthInPixels];

                //this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                //this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorBitmap = new WriteableBitmap(frameDesc.Width, frameDesc.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                //this.depthBitmap = new WriteableBitmap(this.sensor.DepthFrameSource.FrameDescription.Width, this.sensor.DepthFrameSource.FrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);                
                this.colorImg.Source = this.colorBitmap;

                //this.sensor.AllFramesReady += this.sensor_AllFramesReady;
                reader = this.sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth);
                reader.MultiSourceFrameArrived += this.sensor_AllFramesReady;

                try
                {
                    //this.sensor.Start();
                    if (!sensor.IsOpen)
                    {
                        sensor.Open();
                    }
                    
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.outputViewbox.Visibility = System.Windows.Visibility.Collapsed;
                this.txtError.Visibility = System.Windows.Visibility.Visible;
                this.txtInfo.Text = "No Kinect Found";
                
            }

        }

        //private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
            private void sensor_AllFramesReady(object sender, MultiSourceFrameArrivedEventArgs e)
            {
            //BitmapSource depthBmp = null;
            blobCount = 0;
            var tongullFrame = e.FrameReference.AcquireFrame();
      
                //using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                using (var colorFrame = tongullFrame.ColorFrameReference.AcquireFrame())
            {
                //using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                using (var depthFrame = tongullFrame.DepthFrameReference.AcquireFrame())
                {
                    Tongull_DetectBlobs(depthFrame);
                    //end object recogition
                }


                if (colorFrame != null)
                {

                    //colorFrame.CopyPixelDataTo(this.colorPixels);
                    colorFrame.CopyConvertedFrameDataToArray(this.colorPixels, ColorImageFormat.Rgba);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);

                }
            }
        }

        void Tongull_DetectBlobs(DepthFrame depthFrame)
        {
            if (depthFrame == null)
            {
                return;
            }
            //Object recognition
            blobCount = 0;
            //Slicedepthimage is a Custom class
            var depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);

            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

            using (MemStorage stor = new MemStorage())
            {
                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                Contour<System.Drawing.Point> contours = gray_image.FindContours(
                 Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                 Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                 stor);

                for (int i = 0; contours != null; contours = contours.HNext)
                {
                    i++;

                    if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSize.Value, 2)))
                    {
                        MCvBox2D box = contours.GetMinAreaRect();
                        openCVImg.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                        blobCount++;
                    }
                }
            }

            this.outImg.Source = ImageHelpers.ToBitmapSource(openCVImg);
            txtBlobCount.Text = blobCount.ToString();
        }

        #region Window Stuff
        void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }


        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                //this.sensor.Stop();
                this.sensor.Close();
            }
        }

        private void CloseBtnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion
    }
}
