using ReGroup.Common;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Facebook;
using Parse;
using ReGroup.Model;
using Newtonsoft.Json;
using Facebook.Client.Controls;
using ReGroup.utility;

namespace ReGroup
{
    /// <summary>
    /// Pagina vuota che può essere utilizzata autonomamente oppure esplorata all'interno di un frame.
    /// </summary>
    public sealed partial class ContactList : Windows.UI.Xaml.Controls.Page
    {

        public ContactList()
        {
            this.InitializeComponent();
            NavigationHelper navHelper = new NavigationHelper(this);           
        }

        /// <summary>
        /// Richiamato quando la pagina sta per essere visualizzata in un Frame.
        /// </summary>
        /// <param name="e">Dati dell'evento in cui vengono descritte le modalità con cui la pagina è stata raggiunta.
        /// Questo parametro viene in genere utilizzato per configurare la pagina.</param>
        protected async override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            commandBar.IsEnabled = false;
            try
            {
               await FriendRequestAsync();
            }
            catch (Exception we)
            {
                App.FriendsOnMap.Clear();
                Frame.GoBack();
            }
        }

        private void confirm_Click(object sender, RoutedEventArgs e)
        {
            App.FriendsOnMap.Clear();

            foreach (var friend in friendList.SelectedItems)
            {
                App.FriendsOnMap.Add((friend as FBFriend).Id, friend as FBFriend);      
            }
            Frame.GoBack();
        }

        private async System.Threading.Tasks.Task FriendRequestAsync()
        {
            FacebookClient fbClient = new FacebookClient(ParseFacebookUtils.AccessToken);

            //si carica la lista degli amici che ha autorizzato l'app
            dynamic fbFriends = await fbClient.GetTaskAsync("/me/friends");
            var fbFriendsJson = ((object)fbFriends.data).ToString();
            var friendsCollection = JsonConvert.DeserializeObject<System.Collections.Generic.List<FBFriend>>(fbFriendsJson);            

            var queryOnlineUser = await ParseUser.Query.WhereEqualTo("shared", true).WhereNotEqualTo("username", ParseUser.CurrentUser.Username).OrderByDescending("fbId").FindAsync();
            //var queryOnlineUser = await ParseUser.Query.WhereEqualTo("shared", true).OrderByDescending("fbId").FindAsync();
            
            //Si fa un controllo incrociato con la lista di fb e la lista di parse
           var onlineFriend = new System.Collections.Generic.List<FBFriend>();
            foreach (var f in friendsCollection) 
            {
                foreach (var u in queryOnlineUser)
                {
                    if (u.Get<string>("fbId").Equals(f.Id))
                    {
                        onlineFriend.Add(f);
                        break;
                    }
                }                
            }

            if (onlineFriend.Count == 0)
            {
                empty.Visibility = Visibility.Visible;
                commandBar.IsEnabled = true;
                confirm.IsEnabled = false;
                deselect.IsEnabled = false;
            }
            else
            {
                friendList.ItemsSource = onlineFriend;
                
                //Si cerca quelli che erano gi' selezionati
                for (int i = 0; i < friendList.Items.Count; i++)
                {
                    if (App.FriendsOnMap.ContainsKey((friendList.Items[i] as FBFriend).Id))
                    {
                        friendList.SelectedItems.Add(friendList.Items[i]);
                    }                  
                }

                empty.Visibility = Visibility.Collapsed;                
                commandBar.IsEnabled = true;
                confirm.IsEnabled = true;
                deselect.IsEnabled = true;
                friendList.IsEnabled = true;
            }
            loadingRing.IsActive = false;
            progressContainer.Visibility = Visibility.Collapsed;
        }

        private void listView_Loaded(object sender, RoutedEventArgs e)
        {
            progressContainer.Visibility = Visibility.Visible;
            loadingRing.IsActive = true;            
        }

        private void deselect_Click(object sender, RoutedEventArgs e)
        {
            friendList.SelectedItems.Clear();
        }

        private void refresh_Click(object sender, RoutedEventArgs e)
        {
            commandBar.IsEnabled = false;
            friendList.IsEnabled = false;
            progressContainer.Visibility = Visibility.Visible;
            loadingRing.IsActive = true;
            //Si fa senza await perche' una lista e' gia caricata
            FriendRequestAsync();
        }
    }
}
