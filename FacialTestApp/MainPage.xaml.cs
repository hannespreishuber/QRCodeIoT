namespace FacialTestApp
{
    using Newtonsoft.Json;
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Devices.Haptics;
    using Windows.Foundation.Metadata;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public VibrationDevice vib;
        public MainPage()
        {
            this.InitializeComponent();
            this.CanStart = true;

            this.qrcodeWatcher = new QRCodeWatcher(
                new CameraDeviceFinder(
                    deviceInformation =>
                    {
                       return true; //Iot
                       //                      return (deviceInformation.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }
                )
            );
          


        }


        string qrCode;

        public string QrCode
        {
            get => qrCode;
            set
            {
                if (value != this.qrCode)
                {
                    this.qrCode = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public bool CanStart
        {
            get => canStart;
            set
            {
                if (value != this.canStart)
                {
                    this.canStart = value;
                    this.FirePropertyChanged();
                    this.FirePropertyChanged("CanStop");
                }
            }
        }
        public bool CanStop
        {
            get
            {
                return (!this.CanStart);
            }
        }

        async void OnStart()
        {
            if (await VibrationDevice.RequestAccessAsync() != VibrationAccessStatus.Allowed)
            {
                
            }
            vib = await VibrationDevice.GetDefaultAsync();

            this.CanStart = false;
            this.cancelTokenSource = new CancellationTokenSource();
            this.qrcodeWatcher.InFrameQRChanged += OnQRChanged;
            try
            {
                await this.qrcodeWatcher.CaptureAsync(this.cancelTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                this.qrcodeWatcher.InFrameQRChanged -= this.OnQRChanged;
                this.CanStart = true;
            }
        }

        async void  OnQRChanged(object sender, QRChangedEventArgs e)
        {
            this.QrCode = e.QRCode;
            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
                Windows.Phone.Devices.Notification.VibrationDevice.GetDefault().Vibrate(TimeSpan.FromSeconds(1));
            var msg = new ServiceMeldungen();
            msg.Datum = DateTime.Now;
            msg.Meldung = e.QRCode;

            //Task.Run(async () => await CreateMessageAsync(msg));
           await CreateMessageAsync(msg);
           Vibrieren();
        }

        private void Vibrieren()
        {
            try
            {
               
                SimpleHapticsControllerFeedback BuzzFeedback = null;
                foreach (var f in vib.SimpleHapticsController.SupportedFeedback)
                {
                    if (f.Waveform == KnownSimpleHapticsControllerWaveforms.BuzzContinuous)
                        BuzzFeedback = f;
                }
                if (BuzzFeedback != null)
                {
                    vib.SimpleHapticsController.SendHapticFeedbackForDuration(BuzzFeedback, 1, TimeSpan.FromMilliseconds(200));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message +
                "Vibrator nicht zugreifbar");
            }
        }

        void OnStop()
        {
            this.cancelTokenSource.Cancel();
        }
        void FirePropertyChanged([CallerMemberName] string callerName = null)
        {
            this.PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(callerName));
        }
        CancellationTokenSource cancelTokenSource;
        QRCodeWatcher qrcodeWatcher;
        bool canStart;
        async Task<HttpResponseMessage> CreateMessageAsync(ServiceMeldungen msg)
        {
            var client = new HttpClient();
            var json = JsonConvert.SerializeObject(msg);
            var content = new StringContent(json, Encoding.ASCII, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            HttpResponseMessage response = await client.PostAsync(
           "https://iotservice2018.azurewebsites.net/api/servicemeldungens", content);
            response.EnsureSuccessStatusCode();

          

            return response;
        }

        private async void Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
           

                 Windows.UI.Xaml.Application.Current.Exit();
        }
    }
}
