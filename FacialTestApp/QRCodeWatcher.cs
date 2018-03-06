using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;

namespace FacialTestApp
{
    public class QRChangedEventArgs : EventArgs
    {
        internal QRChangedEventArgs(string newQR)
        {
            this.QRCode = newQR;
        }
        public string QRCode { get; private set; }
    }
    public interface IQRWatcher
    {
        event EventHandler<QRChangedEventArgs> InFrameQRChanged;
    }
    class QRCodeWatcher : IQRWatcher
    {
        public event EventHandler<QRChangedEventArgs> InFrameQRChanged;

        public QRCodeWatcher(CameraDeviceFinder deviceFinder,
        bool ignoreSyncContext = false)
        {
            if (!ignoreSyncContext)
            {
                this.syncContext = SynchronizationContext.Current;
            }
            this.deviceFinder = deviceFinder;
        }
        BarcodeReader scanner = new ZXing.BarcodeReader();

        public async Task CaptureAsync(CancellationToken cancelToken)
        {
            if (this.capturing)
            {
                throw new InvalidOperationException("Already capturing");
            }
            this.capturing = true;

            try
            {
                // Which device are we wanting to pull frames from?
                var device = await this.deviceFinder.FindSingleCameraAsync();

                MediaCaptureInitializationSettings initialisationSettings = new MediaCaptureInitializationSettings()
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    VideoDeviceId = device.Id,
                    // This turns out to be more important than I thought if I want a SoftwareBitmap
                    // back on each frame
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };
                // Initialise the media capture
                using (var mediaCapture = new MediaCapture())
                {
                    await mediaCapture.InitializeAsync(initialisationSettings);

                    // Get a frame reader.
                    using (var frameReader = await mediaCapture.CreateFrameReaderAsync
                        (
                            mediaCapture.FrameSources.First(
                                fs =>
                                (
                                    (fs.Value.Info.DeviceInformation.Id == device.Id) &&
                                    (fs.Value.Info.MediaStreamType == MediaStreamType.VideoPreview) &&
                                    (fs.Value.Info.SourceKind == MediaFrameSourceKind.Color)
                                )
                            ).Value
                        )
                    )
                    {
                        int handlingFrame = 0;
                        TimeSpan? lastFrameTime = null;
                        string lastqrResult = "";

                        frameReader.FrameArrived += async (s, e) =>
                        {
                            if (Interlocked.CompareExchange(ref handlingFrame, 1, 0) == 0)
                            {
                                using (var frame = frameReader.TryAcquireLatestFrame())
                                {
                                    if (frame!=null && frame?.SystemRelativeTime != lastFrameTime)  //null check?
                                    {
                                        lastFrameTime = frame.SystemRelativeTime;

                                        var originalBitmap = frame.VideoMediaFrame.SoftwareBitmap;
                                        var bitmapForDetection = originalBitmap;

                                        if (originalBitmap != null)
                                        {
                                            //// Run detection on this...
                                            SoftwareBitmap sb = SoftwareBitmap.Convert(
                                                   originalBitmap, originalBitmap.BitmapPixelFormat);
                                            bitmapForDetection.CopyTo(sb);
                                            Result qrResults = scanner.Decode(sb);
                                           
                                            if (qrResults!=null && qrResults.Text != lastqrResult)
                                            {
                                                this.DispatchQRChanged(qrResults.Text);
                                                lastqrResult = qrResults.Text;
                                            }
                                        }
                                        //if (bitmapForDetection != originalBitmap)
                                        //{
                                        //    bitmapForDetection.Dispose();
                                        //}
                                        originalBitmap.Dispose();
                                    }
                                }
                                Interlocked.Exchange(ref handlingFrame, 0);
                            }
                        };
                        await frameReader.StartAsync();

                        await Task.Delay(-1, cancelToken);
                    }
                }
            }
            finally
            {
                this.capturing = false;
            }
        }
        void DispatchQRChanged(string msg)
        {
            if (this.syncContext != null)
            {
                this.syncContext.Post(
                    _ =>
                    {
                        
                        this.FireQRChanged(msg);
                    },
                    null);
            }
            else
            {
                this.FireQRChanged(msg);
            }
        }
        void FireQRChanged(string msg)
        {
            this.InFrameQRChanged?.Invoke(
                this, new QRChangedEventArgs(msg));
        }
        SynchronizationContext syncContext;
        CameraDeviceFinder deviceFinder;
        volatile bool capturing;
    }
}

