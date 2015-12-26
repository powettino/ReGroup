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
    }
}
