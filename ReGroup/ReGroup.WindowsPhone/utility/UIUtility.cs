using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReGroup.Model;
using Windows.UI.Popups;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Xaml;

namespace ReGroup.utility
{

    class UIUtility
    {
        //Metodo per visualizzare finestre di dialogo con o senza titolo (e' opzionale)
        public async static void showDialog(string testo, string titolo = "")
        {
            if (titolo.Length == 0)
            {
                await new MessageDialog(testo).ShowAsync();
            }
            else
            {
                await new MessageDialog(testo, titolo).ShowAsync();
            }
        }

        public async static Task<bool> showDialogWithButton(string testo, string titolo)
        {
            var dialog = new MessageDialog(testo, titolo);
            dialog.Commands.Add(new UICommand { Label = "Yes", Id = 0 });
            dialog.Commands.Add(new UICommand { Label = "Cancel", Id = 1 });
            var res = await dialog.ShowAsync();
            return ((int)res.Id) == 0;
        }

        public static void ShowToast(Grid layout, string message)
        {
            //var toastString = string.Format("<toast><visual version='1'><binding template='ToastText1'><text id='1'>{0}</text></binding></visual></toast>", message);
            //var doc = new Windows.Data.Xml.Dom.XmlDocument();
            //doc.LoadXml(toastString);
            //var toast = new ToastNotification(doc);
            //ToastNotificationManager.CreateToastNotifier().Show(toast);
            Grid grid = new Grid();
            grid.Width = 300;
            grid.Height = 60;
            grid.Background = new SolidColorBrush(Colors.Transparent);
            grid.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            grid.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
            grid.Margin = new Windows.UI.Xaml.Thickness(0, 15, 0, 0);

            TextBlock text = new TextBlock();
            text.Text = message;
            text.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            text.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            text.FontSize = 20;

            grid.Children.Add(text);

            layout.Children.Add(grid);

            DispatcherTimer t = new DispatcherTimer();
            t.Interval = new TimeSpan(0, 0, 3);
            t.Tick += (sender, args) =>
                {
                    layout.Children.Remove(grid);
                    t.Stop();
                };
            t.Start();
        }
    }
}
