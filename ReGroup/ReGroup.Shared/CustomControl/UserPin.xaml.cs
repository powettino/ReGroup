using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

// Il modello di elemento per il controllo utente è documentato all'indirizzo http://go.microsoft.com/fwlink/?LinkId=234236

namespace ReGroup.CustomControl
{
    public sealed partial class UserPin : UserControl
    {
        private const double EARTH_RADIUS = 6378137;
        private const double _raggio = 200; 

        public UserPin()
        {
            this.InitializeComponent();
            Logo.Fill = new ImageBrush
            {
                Stretch = Stretch.Uniform,
                ImageSource = new BitmapImage()
                {
                    UriSource = new Uri("ms-appx:///Assets/Map/pin.png")
                }
            };
        }

        public void SetRadius(double latitudine, double zoom)
        {
            
            double risoluzione = Math.Cos(latitudine * Math.PI / 180) * 2 * Math.PI * EARTH_RADIUS / (256 * Math.Pow(2, zoom));
            double pixel = (_raggio / risoluzione);
            Area.Width = pixel;
            Area.Height = pixel;
            Area.Margin = new Thickness(-pixel / 2, -pixel / 2, 0, 0);
        }

    }
}

