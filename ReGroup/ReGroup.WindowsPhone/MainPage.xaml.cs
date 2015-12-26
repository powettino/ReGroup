using Facebook;
using Parse;
using ReGroup.Common;
using ReGroup.utility;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using ReGroup.Model;
using Windows.UI;
using ReGroup.CustomControl;

namespace ReGroup
{
    /// <summary>
    /// Pagina vuota che può essere utilizzata autonomamente oppure esplorata all'interno di un frame.
    /// </summary>
    public sealed partial class MainPage : Page, IWebAuthenticationContinuable
    {
        const uint _accuratezza = 20;
        const uint _sogliaMov = 5;
        const uint _defaultZoom = 15;
        const uint _report = 2000;
        const int _timerFire = 5;

        NavigationHelper navigationHelper;
        DispatcherTimer timer;

        Share sharing = Share.off;

        Geolocator _geoLoc;
        ConnectionUtility connUtils = new ConnectionUtility();
        ParseObject shared;

        Geopoint currentCenterMap;

        UserPin user;

        private enum Share
        {
            off, friend, user
        };

        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        public MainPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            //this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            //this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            App.FriendsOnMap = new System.Collections.Generic.Dictionary<string, FBFriend>();

            timer = new DispatcherTimer()
            {
                Interval = new TimeSpan(0, 0, _timerFire)
            };

            //Si inizializza con reportinterval per avere sempre 
            _geoLoc = new Geolocator
            {
                //ReportInterval = _report,
                DesiredAccuracyInMeters = _accuratezza,
                MovementThreshold = _sogliaMov,
                //DesiredAccuracy = PositionAccuracy.Default
            };
        }

        //void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        //{
        //    Debug.WriteLine("Sono nella load");
        //}

        //void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        //{
        //}

        private void clearMapChildren()
        {
            foreach (var element in map.Children.ToList())
            {
                if (!(element is UserPin))
                {
                    map.Children.Remove(element);
                }
            }
        }

        /// Si aggiorna la posizione degli amici, si separa dalla posizione del'utente
        /// per mantenere la visualizzazione il piu' possibile
        private void updateMapFriendPosition()
        {
            var existing = new System.Collections.Generic.Dictionary<string, Geopoint>();
            var removed = new System.Collections.Generic.HashSet<string>();
            var moved = new System.Collections.Generic.HashSet<string>();

            ///In tutti i figli della mappa si cerca gli ellissi che sono gli amici
            ///per eventualmente muoverli senza vedere l'effetto flicker
            foreach (var element in map.Children.ToList())
            {
                if (element is Windows.UI.Xaml.Shapes.Ellipse)
                {
                    //Se esiste si recupera l'oggetto e la posizione e si sposta il figlio della mappa
                    // si aggiunge l'elemento alla collezione di supporto
                    Windows.UI.Xaml.Shapes.Ellipse pin = element as Windows.UI.Xaml.Shapes.Ellipse;
                    if (App.FriendsOnMap.ContainsKey(pin.Tag as string))
                    {
                        FBFriend friend = App.FriendsOnMap[pin.Tag as string];
                        if (friend.Geopoint != null)
                        {
                            MapControl.SetLocation(pin, friend.Geopoint);
                        }
                        existing.Add(friend.Id, friend.Geopoint);
                    }
                    else
                    {
                        //Va rimosso perche' non c'e piu'
                        map.Children.Remove(pin);
                        removed.Add(pin.Tag as string);
                    }
                }
                else if (element is StackPanel)
                {
                    ///Se era aperto il dettaglio della posizione o si rimuove o si sposta 
                    ///contestualmente al cerchio che rappresenta l'amico
                    StackPanel b = element as StackPanel;
                    if (removed.Contains((b.Tag) as string))
                    {
                        map.Children.Remove(element);
                    }
                    if (existing.ContainsKey((b.Tag) as string))
                    {
                        MapControl.SetLocation(element, existing[b.Tag as string]);
                    }
                }
            }

            ///se la lista locale e' piu' grande di quella degli esistenti vuol dire 
            ///che sono stati aggiunti amici nuovi alla mappa
            if (App.FriendsOnMap.Count > existing.Count)
            {
                foreach (FBFriend element in App.FriendsOnMap.Values)
                {
                    if (!existing.ContainsKey(element.Id))
                    {
                        //Trovato elemento non precedentemente presente e si aggiunge
                        if (element.Geopoint != null)
                        {
                            //FriendPin fence = new FriendPin();
                            //fence.Image = new BitmapImage(element.Picture);
                            //fence.Id = element.Id;

                            var fence = new Windows.UI.Xaml.Shapes.Ellipse()
                            {
                                Width = 30,
                                Height = 30,
                                Stroke = new SolidColorBrush(Colors.Black),
                                StrokeThickness = 2,
                                Tag = element.Id,
                                Name = element.Name
                            };
                            fence.Fill = new ImageBrush()
                            {
                                ImageSource = new Windows.UI.Xaml.Media.Imaging.BitmapImage(element.Picture),
                                Stretch = Stretch.Uniform
                            };
                            fence.Tapped += fence_Tapped;
                            //fence.Tapped += fence_Tapped;
                            map.Children.Add(fence);
                            toast.Duration = 2;
                            toast.Message = "Added " + element.Name;
                            MapControl.SetLocation(fence, element.Geopoint);
                            MapControl.SetNormalizedAnchorPoint(fence, new Windows.Foundation.Point(0.5, 0));
                            //map.Center = element.Geopoint;
                        }
                    }
                }
            }
        }

        //Metodo per aggiornare la posizione dell'utente
        private async Task updateMapUserPosition(Geopoint userGeo)
        {
            //la prima volta si crea, le altre si aggiorna la posizione per non fare il flicker
            if (user == null)
            {
                user = new UserPin();
                user.SetRadius(userGeo.Position.Latitude, map.ZoomLevel);
                map.Children.Add(user);
                MapControl.SetLocation(user, userGeo);
                MapControl.SetNormalizedAnchorPoint(user, new Windows.Foundation.Point(0.5, 1.0));              
            }
            else
            {
                //(map.MapElements[0] as MapIcon).Location = userGeo;
                //foreach (var element in map.Children)
                //{
                //    if (element is UserPin)
                //    {
                //        (element as UserPin).SetRadius(userGeo.Position.Latitude, map.ZoomLevel);
                //        MapControl.SetLocation(element, userGeo);
                //    }
                //}
                user.SetRadius(userGeo.Position.Latitude, map.ZoomLevel);
                MapControl.SetLocation(user, userGeo);
            }

            //Se non era ancora stato centrato si centra e si salva l-ultima locazione trovata per evitare di dover continuamente
            // invocare il gps e utilizzare quella visto che l'aggiornamento viene fatto sulla soglia di movimento
            if (currentCenterMap == null && userGeo != null)
            {
                await map.TrySetViewAsync(userGeo, _defaultZoom);
                //centered = true;
            }
           
            currentCenterMap = userGeo;
        }

        //si apre o chiude il dettaglio utente
        async void fence_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string clickedId = ((sender as Windows.UI.Xaml.Shapes.Ellipse).Tag) as string;
            string name = ((sender as Windows.UI.Xaml.Shapes.Ellipse).Name) as string;
            if (App.FriendsOnMap.ContainsKey(clickedId))
            {
                bool opened = false;
                foreach (var element in map.Children.ToList())
                {
                    if (element is StackPanel)
                    {
                        if (((element as StackPanel).Tag as string).Equals(clickedId))
                        {
                            opened = true;
                            map.Children.Remove(element);
                            break;
                        }
                    }
                }

                if (!opened)
                {
                    //si fa reverse geocoding della locazione
                    Geopoint point = App.FriendsOnMap[clickedId].Geopoint;
                    var locRes = await Windows.Services.Maps.MapLocationFinder.FindLocationsAtAsync(point);
                    if (locRes.Status == Windows.Services.Maps.MapLocationFinderStatus.Success)
                    {
                        var add = locRes.Locations[0].Address;
                        var externalBox = createLocationBox(name, clickedId, add.Country, add.Region, add.Town, add.Street, add.StreetNumber, new Geopoint(new BasicGeoposition()
                            {
                                Longitude = point.Position.Longitude,
                                Latitude = point.Position.Latitude
                            })
                        );
                        map.Children.Add(externalBox);
                        MapControl.SetLocation(externalBox, point);
                        MapControl.SetNormalizedAnchorPoint(externalBox, new Windows.Foundation.Point(0.5, 1));


                        DispatcherTimer clearFence = new DispatcherTimer();
                        clearFence.Interval = new TimeSpan(0, 0, 5);
                        clearFence.Tick += (timer, args) =>
                        {
                            map.Children.Remove(externalBox);
                            clearFence.Stop();
                        };
                        clearFence.Start();
                        //map.Center = point;
                    }
                }
            }
        }

        private StackPanel createLocationBox(string name, string id, string country, string region, string city, string address, string addressNumber, Geopoint point)
        {
            StackPanel box = new StackPanel();
            box.Name = "box" + id;
            box.Background = new SolidColorBrush(Colors.Transparent);
            box.Orientation = Orientation.Vertical;

            TextBlock nome = new TextBlock();
            nome.Name = name + id;
            nome.Text = name;
            nome.HorizontalAlignment = HorizontalAlignment.Center;
            nome.VerticalAlignment = VerticalAlignment.Center;
            nome.Padding = new Thickness(5, 5, 5, 7);
            nome.FontSize = 24;
            nome.FontWeight = Windows.UI.Text.FontWeights.Bold;

            TextBlock nazione = new TextBlock();
            nazione.Name = "country" + id;
            nazione.Text = "Country: " + (country.Equals("") ? " - " : country);
            nazione.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            nazione.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            nazione.Padding = new Thickness(5, 5, 5, 5);
            nazione.FontSize = 20;
            nazione.Foreground = new SolidColorBrush(Colors.AntiqueWhite);

            TextBlock regione = new TextBlock();
            regione.Name = "region" + id;
            regione.Text = "Region: " + (region.Equals("") ? " - " : region);
            regione.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            regione.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            regione.FontSize = 20;
            regione.Padding = new Thickness(5, 0, 5, 5);
            regione.Foreground = new SolidColorBrush(Colors.AntiqueWhite);

            TextBlock citta = new TextBlock();
            citta.Name = "citta" + id;
            citta.Text = "City: " + (city.Equals("") ? " - " : city);
            citta.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            citta.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            citta.Padding = new Thickness(5, 0, 5, 5);
            citta.FontSize = 18;
            citta.Foreground = new SolidColorBrush(Colors.AntiqueWhite);

            TextBlock indirizzo = new TextBlock();
            indirizzo.Name = "indirizzo" + id;
            indirizzo.Text = "Address: " + (address.Equals("") ? " - " : (addressNumber.Equals("") ? address : address + ", " + addressNumber));
            indirizzo.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            indirizzo.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            indirizzo.FontSize = 20;
            indirizzo.Padding = new Thickness(5, 0, 5, 5);
            indirizzo.Foreground = new SolidColorBrush(Colors.AntiqueWhite);

            box.Children.Add(nome);
            box.Children.Add(nazione);
            box.Children.Add(regione);
            box.Children.Add(citta);
            box.Children.Add(indirizzo);

            Border border = new Border();
            border.BorderThickness = new Thickness(5);
            border.CornerRadius = new CornerRadius(15);
            border.Child = box;
            border.Background = new SolidColorBrush(Colors.DarkCyan);
            border.Padding = new Thickness(3, 3, 3, 3);

            TextBlock fake = new TextBlock();
            fake.Padding = new Thickness(0, 0, 0, 0);
            fake.Text = " ";

            StackPanel externalBox = new StackPanel();
            externalBox.Tag = id;
            externalBox.Background = new SolidColorBrush(Colors.Transparent);
            externalBox.Orientation = Orientation.Vertical;

            externalBox.Children.Add(border);
            externalBox.Children.Add(fake);

            return externalBox;
        }

        private async void geoLoc_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            Geopoint point = args.Position.Coordinate.Point;

            //Debug.WriteLine(System.DateTime.Now + ": cambiato di posizione con argomenti " + point.Position.Latitude + " - " + point.Position.Longitude);

            //Viene eseguito senza await perche' anche se qualche posizione viene saltata si sta in un intorno accettabile
            shareUserPosition(point);

            ///si esegue l'aggiornamento della mappa utilizzando il dispatcher poiche' la UI viene eseguita da un thread diverso
            ///da quello che esegue il position changed (che dovrebbe essere in background)
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(
                async () =>
                {
                    await updateMapUserPosition(point);
                }));
        }

        //metodo per il login attraverso il webauthenticationbroker
        private async Task LoginAsync(Boolean local)
        {
            var redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().ToString();
            var fb = new FacebookClient();
            var loginUrl = fb.GetLoginUrl(new
            {
                client_id = App.FBClientID,
                redirect_uri = redirectUri,
                response_type = "token",
                scope = App.FBScope
            });

            Uri startUri = loginUrl;
            Uri endUri = new Uri(redirectUri, UriKind.Absolute);

            WebAuthenticationBroker.AuthenticateAndContinue(startUri, endUri, null, WebAuthenticationOptions.None);
        }

        //quando il webauthenticationbroken restituisce il risultato
        public async void ContinueWebAuthentication(Windows.ApplicationModel.Activation.WebAuthenticationBrokerContinuationEventArgs args)
        {
            try
            {
                await ParseAuthenticationResultAsync(args.WebAuthenticationResult);
            }
            catch (Exception e)
            {
                UIUtility.showDialog("Generic error: " + e.Message, "Error");
            }
        }

        public async Task ParseAuthenticationResultAsync(WebAuthenticationResult result)
        {
            switch (result.ResponseStatus)
            {
                case WebAuthenticationStatus.ErrorHttp:
                    UIUtility.showDialog("Connection error");
                    //Debug.WriteLine("Connection error");
                    break;
                case WebAuthenticationStatus.Success:
                    Uri uri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
                    string[] res = result.ResponseData.Split('&');

                    if (!res[0].Contains("error"))
                    {
                        var pattern = string.Format("{0}#access_token={1}&expires_in={2}", uri, "(?<access_token>.+)",
                          "(?<expires_in>.+)");

                        var match = System.Text.RegularExpressions.Regex.Match(result.ResponseData, pattern);

                        var access_token = match.Groups["access_token"];
                        var expires_in = match.Groups["expires_in"];

                        facebookLogin.Visibility = Visibility.Collapsed;
                        showLoading(true);

                        ///recuperate le informazioni di auth si recupera le info dell'utente fb
                        FacebookClient client = new FacebookClient(access_token.Value);
                        dynamic user = await client.GetTaskAsync("me");

                        var user_id = ((object)user.id).ToString();

                        ParseUser u = await ParseFacebookUtils.LogInAsync(user_id, access_token.Value, DateTime.Now.AddSeconds(double.Parse(expires_in.Value)));
                        ParseUser.CurrentUser["fbId"] = user.id;
                        ParseUser.CurrentUser["name"] = user.name;
                        ParseUser.CurrentUser["shared"] = false;
                        await ParseUser.CurrentUser.SaveAsync();

                        //si recupera la prima posizione in assoluto
                        Geoposition position = await _geoLoc.GetGeopositionAsync();
                        showLoading(false);

                        updateMapUserPosition(position.Coordinate.Point);                        

                        //sottoscrivo l'evento per il cambio posizione per aggiornare parse
                        _geoLoc.PositionChanged += geoLoc_PositionChanged;
                    }
                    else
                    {
                        //Debug.WriteLine("Login error: " + (res[2].Split('='))[1] + " - Reason " + (res[3].Split('='))[1], "Login failed");
                        UIUtility.showDialog("Login error: " + (res[2].Split('='))[1] + " - Reason " + (res[3].Split('='))[1], "Login failed");
                    }

                    break;
                case WebAuthenticationStatus.UserCancel:
                    if (ParseUser.CurrentUser != null)
                    {
                        //Debug.WriteLine("Operation aborted");
                        UIUtility.showDialog("Operation aborted from user", "Error");
                    }
                    break;
                default:
                    break;
            }
        }

        /// Richiamato quando la pagina sta per essere visualizzata in un Frame.
        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            //Debug.WriteLine("Sono entrato nel navHelper di main");
            this.navigationHelper.OnNavigatedTo(e);

            ///se e' una pagina nuova 
            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
            {
                commandBar.IsEnabled = false;
                //se non esiste un account loggato mostro il pulsante di login
                oscuramentoMap.Visibility = Visibility.Visible;
                if (ParseUser.CurrentUser == null || ParseFacebookUtils.AccessToken == null)
                {
                    facebookLogin.Visibility = Visibility.Visible;
                }
                else
                {
                    //altrimenti recupero la posizione e la mostro
                    //Debug.WriteLine("Utente gia' loggato");                       
                    facebookLogin.Visibility = Visibility.Collapsed;
                    showLoading(true);
                    Geoposition position = await _geoLoc.GetGeopositionAsync();
                    showLoading(false);
                    updateMapUserPosition(position.Coordinate.Point);

                    ////sottoscrivo l'evento per il cambio posizione per aggiornare parse
                    _geoLoc.PositionChanged += geoLoc_PositionChanged;
                }
            }
            //se stiamo tornando dalla navigazione tra pagine
            else if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.Back)
            {
                ///se non c'e' niente di selezionato pulisco l'interfaccia e stoppo i timer
                sharePositionButton.IsEnabled = App.FriendsOnMap.Count != 0 ? false : true;
                facebookLogin.Visibility = Visibility.Collapsed;
                //showLoading(false);
                showLoading(true);
                Geoposition position = await _geoLoc.GetGeopositionAsync();

                updateMapUserPosition(position.Coordinate.Point);
                //sottoscrivo l'evento per il cambio posizione per aggiornare parse
                _geoLoc.PositionChanged += geoLoc_PositionChanged;
                if (App.FriendsOnMap.Count == 0)
                {
                    clearMapChildren();
                    timer.Stop();
                    timer.Tick -= friend_Tick;
                    if (shared != null)
                    {
                        await shared.DeleteAsync();
                        shared = null;
                    }
                    if (currentCenterMap != null)
                    {
                       await map.TrySetViewAsync(currentCenterMap);
                    }
                   
                    showLoading(false);
                }
                ///se c'e' qualcosa di selezionato aggiunto il tick e start del timer
                else if (App.FriendsOnMap.Count != 0 && shared == null)
                {
                    //showLoading(true);
                    shared = new ParseObject("SharedPoints");
                    sharing = Share.friend;
                    await shareUserPosition(currentCenterMap);
                    showLoading(false);

                    timer.Tick += friend_Tick;

                    timer.Start();
                }
            }
        }

        ///questo metodo viene chiamato quando si lascia questa pagina, e quindi viene chiamato anche quando va in sospensione!!!
        protected async override void OnNavigatedFrom(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
            //Debug.WriteLine("Ritorno ora da Navigate");

            timer.Stop();
            timer.Tick -= share_Tick;
            timer.Tick -= friend_Tick;
            sharing = Share.off;
            if (sharePositionButton.Label.Equals("Unshare"))
            {
                sharePositionButton.Icon = new SymbolIcon(Symbol.ReShare);
                sharePositionButton.Label = "Share";
                handleFriendButton.IsEnabled = true;
            }
            //se procede in avanti sta andando in sospensione!
            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.Forward)
            {
                clearMapChildren();
                App.FriendsOnMap.Clear();
                map.Center = currentCenterMap;
                sharePositionButton.IsEnabled = true;
                commandBar.IsEnabled = ParseUser.CurrentUser != null ? true : false;           
            }
            if (shared != null)
            {
                await shared.DeleteAsync();
                shared = null;
                ParseUser.CurrentUser["shared"] = false;
                await ParseUser.CurrentUser.SaveAsync();
            }
        }

        private async void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (ConnectionUtility.HasInternetAccess)
            {
                await LoginAsync(false);
            }
            else
            {
                UIUtility.showDialog("No connection internet available", "No connection");
            }
        }

        private void addFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionUtility.HasInternetAccess)
            {
                Frame.Navigate(typeof(ContactList));
            }
            else
            {
                UIUtility.showDialog("No connection internet available", "No connection");
            }
        }

        private async void sharePositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionUtility.HasInternetAccess)
            {
                await ShareAction();
            }
            else
            {
                UIUtility.showDialog("No connection internet available", "No connection");
            }
        }

        private async Task ShareAction()
        {
            try
            {
                if (sharePositionButton.Label.Equals("Share"))
                {
                    if (await UIUtility.showDialogWithButton("Are you sure you want share your position?", "Share"))
                    {
                        showLoading(true);
                        ParseUser.CurrentUser["shared"] = true;
                        await ParseUser.CurrentUser.SaveAsync();
                        App.FriendsOnMap.Clear();
                        shared = new ParseObject("SharedPoints");
                        sharing = Share.user;
                        await shareUserPosition(currentCenterMap);
                        showLoading(false);
                        sharePositionButton.Icon = new SymbolIcon(Symbol.DisableUpdates);
                        sharePositionButton.Label = "Unshare";
                        handleFriendButton.IsEnabled = false;
                        timer.Tick += share_Tick;
                        timer.Start();
                    }
                }
                else
                {
                    if (await UIUtility.showDialogWithButton("Are you sure to obscure your position?", "Unshare"))
                    {
                        showLoading(true);
                        sharing = Share.off;
                        timer.Stop();
                        timer.Tick -= share_Tick;
                        ParseUser.CurrentUser["shared"] = false;
                        await ParseUser.CurrentUser.SaveAsync();
                        await shared.DeleteAsync();
                        shared = null;
                        clearMapChildren();
                        App.FriendsOnMap.Clear();
                        sharePositionButton.Icon = new SymbolIcon(Symbol.ReShare);
                        sharePositionButton.Label = "Share";
                        handleFriendButton.IsEnabled = true;
                        showLoading(false);
                    }
                }
            }
            catch (System.Net.WebException)
            {
                showLoading(false);
                toast.Duration = 5;
                toast.Message = "Connection lost";
            }
        }

        /// metodo per condividere la posizione su parse, nel caso sia ricerca di amici condivide
        /// anche la proprio posizione con gli amici aggiunti, altrimenti la condivide con tutti
        private async Task shareUserPosition(Geopoint geo)
        {
            if (shared != null)
            {
                shared["user"] = ParseUser.CurrentUser["fbId"];
                if (sharing == Share.friend)
                {
                    shared.AddRangeUniqueToList("with", App.FriendsOnMap.Keys.ToArray());
                }
                shared["location"] = new ParseGeoPoint(geo.Position.Latitude, geo.Position.Longitude);             
                await shared.SaveAsync();
                //Debug.WriteLine("salvata la posizione");
            }
        }

        async void share_Tick(object sender, object e)
        {
            try
            {
                //si cerca tutti gli amici che hanno aggiunto l'utente  
                var queryResult = await ParseObject.GetQuery("SharedPoints").WhereEqualTo("with", ParseUser.CurrentUser["fbId"]).FindAsync();

               var tempHash = new System.Collections.Generic.HashSet<string>();
                foreach (var element in queryResult)
                {
                    //si recupera l;oggetto utente (amico)
                    var user = await ParseUser.Query.WhereEqualTo("fbId", element["user"]).FirstAsync();
                    // si recupera la posizione della condivisione
                    var point = element.Get<ParseGeoPoint>("location");
                    Geopoint geo = new Geopoint(new BasicGeoposition()
                        {
                            Latitude = point.Latitude,
                            Longitude = point.Longitude
                        });
                    //si crea un nuovo @amico@ con le info recuperate

                    var id = user.Get<string>("fbId");
                    tempHash.Add(id);

                    //se la lista non lo conteneva lo aggiunge altrimenti si aggiorna solo la posizione
                    if (!App.FriendsOnMap.ContainsKey(id))
                    {
                        FBFriend temp_friend = new FBFriend(user.Get<string>("name"), id)
                        {
                            Geopoint = geo
                        };
                        App.FriendsOnMap.Add(temp_friend.Id, temp_friend);
                    }
                    else
                    {
                        App.FriendsOnMap[id].Geopoint = geo;
                    }
                }

                //se la lista contiene piu' elementi di quella appena presa da parse ci sono amici da rimuovere
                if (App.FriendsOnMap.Count > tempHash.Count)
                {
                    foreach (var key in App.FriendsOnMap.Keys.ToList())
                    {
                        if (!tempHash.Contains(key))
                        {
                            toast.Duration = 3;
                            toast.Message = App.FriendsOnMap[key].Name + " has removed his sharing";
                            App.FriendsOnMap.Remove(key);
                        }
                    }
                }
                if (shared != null)
                {
                    updateMapFriendPosition();
                }
                else
                {
                    App.FriendsOnMap.Clear();
                    clearMapChildren();
                }
            }
            catch (System.Net.WebException)
            {
                //Debug.WriteLine("Errore nel tick di condivisione");
                timer.Stop();
                timer.Tick -= share_Tick;

                if (shared != null)
                {
                    toast.Duration = 5;
                    toast.Message = "Connection lost";
                    clearMapChildren();
                    App.FriendsOnMap.Clear();
                    sharePositionButton.Icon = new SymbolIcon(Symbol.ReShare);
                    sharePositionButton.Label = "Share";
                    handleFriendButton.IsEnabled = true;
                    shared = null;
                }
            }

        }

        async void friend_Tick(object sender, object e)
        {
            try
            {
                //showLoading(false);

                //Debug.WriteLine("Cerco punto da aggiungere");
                //si cerca tutte le posizione degli amici aggiunti
                var queryResult = await ParseObject.GetQuery("SharedPoints").WhereContainedIn("user", App.FriendsOnMap.Keys.ToArray()).FindAsync();

                var tempHash = new System.Collections.Generic.HashSet<string>();
                foreach (var element in queryResult)
                {
                    if (App.FriendsOnMap.ContainsKey(element.Get<string>("user")))
                    {
                        //si aggiorna la posizione nella lista
                        tempHash.Add(element.Get<string>("user"));
                        var point = element.Get<ParseGeoPoint>("location");
                        App.FriendsOnMap[element.Get<string>("user")].Geopoint = new Geopoint(new BasicGeoposition()
                        {
                            Latitude = point.Latitude,
                            Longitude = point.Longitude
                        });
                    }
                }

                //Si rimuove le chiavi obsolete che sono state rimosse dalla condivisione
                if (App.FriendsOnMap.Count > tempHash.Count)
                {
                    foreach (var key in App.FriendsOnMap.Keys.ToList())
                    {
                        if (!tempHash.Contains(key))
                        {
                            toast.Duration = 3;
                            toast.Message = App.FriendsOnMap[key].Name + " has removed his sharing";
                            App.FriendsOnMap.Remove(key);
                        }
                    }
                }

                updateMapFriendPosition();

                //Se non ci sono piu' amici disponibili si annulla la ricerca.
                if (App.FriendsOnMap.Count == 0)
                {
                    timer.Stop();
                    timer.Tick -= friend_Tick;
                    toast.Duration = 5;
                    if (!sharePositionButton.IsEnabled)
                    {
                        toast.Message = "All the selected friends have removed their sharing";
                        sharePositionButton.IsEnabled = true;
                    }
                    if (shared != null)
                    {
                        await shared.DeleteAsync();
                        shared = null;
                    }
                }
            }
            catch
            {
                timer.Stop();
                timer.Tick -= friend_Tick;
                if (shared != null)
                {
                    toast.Duration = 5;
                    clearMapChildren();
                    App.FriendsOnMap.Clear();
                    toast.Message = "Connection lost";
                    sharePositionButton.IsEnabled = true;
                    shared = null;
                }
                return;
            }
        }

        private async void logout_Click(object sender, RoutedEventArgs e)
        {
            if (await UIUtility.showDialogWithButton("Do you really want to logout?", "Logout"))
            {
                if (ConnectionUtility.HasInternetAccess)
                {
                    sharing = Share.off;
                    timer.Stop();
                    timer.Tick -= friend_Tick;
                    timer.Tick -= share_Tick;
                    showLoading(true);
                    try
                    {
                        ParseUser.CurrentUser["shared"] = false;
                        await ParseUser.CurrentUser.SaveAsync();
                        if (shared != null)
                        {
                            await shared.DeleteAsync();
                            shared = null;
                        }

                        var fb = new FacebookClient();
                        Uri uri = fb.GetLogoutUrl(new
                            {
                                access_token = ParseFacebookUtils.AccessToken,
                                next = "https://www.facebook.com/connect/login_success.html"
                            });
                        var redirectUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().ToString();
                        Uri endUri = new Uri(redirectUri, UriKind.Absolute);
                        WebAuthenticationBroker.AuthenticateAndContinue(uri, endUri, null, WebAuthenticationOptions.None);

                        await ParseUser.LogOutAsync();

                        _geoLoc.PositionChanged -= geoLoc_PositionChanged;
                        showLoading(false);
                        facebookLogin.Visibility = Visibility.Visible;
                        oscuramentoMap.Visibility = Visibility.Visible;
                        map.MapElements.Clear();
                        commandBar.IsEnabled = false;

                    }
                    catch (System.Net.WebException)
                    {
                        showLoading(false);
                        toast.Duration = 5;
                        toast.Message = "Connection lost";
                    }

                    sharePositionButton.Icon = new SymbolIcon(Symbol.ReShare);
                    sharePositionButton.Label = "Share";
                    handleFriendButton.IsEnabled = true;
                    sharePositionButton.IsEnabled = true;
                    if (ParseUser.CurrentUser == null)
                    {
                        commandBar.IsEnabled = false;
                    }

                    //clearMapChildren();
                    //qua si pulisce tutto per rimuovere anche la posizione dell'utente;
                    map.Children.Clear();
                    App.FriendsOnMap.Clear();
                }
                else
                {
                    UIUtility.showDialog("No connection internet available", "No connection");
                }
            }
        }

        private void showLoading(bool show)
        {
            if (show)
            {
                oscuramentoMap.Visibility = Visibility.Visible;
                commandBar.IsEnabled = false;
                loadingRing.IsActive = true;
                progressContainer.Visibility = Visibility.Visible;
            }
            else
            {
                oscuramentoMap.Visibility = Visibility.Collapsed;
                commandBar.IsEnabled = true;
                loadingRing.IsActive = false;
                progressContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void centerMap_Click(object sender, RoutedEventArgs e)
        {
            map.TrySetViewAsync(currentCenterMap);
        }

        private async void map_MapHolding(MapControl sender, MapInputEventArgs args)
        {
            if (ConnectionUtility.HasInternetAccess)
            {
                await ShareAction();
            }
            else
            {
                UIUtility.showDialog("No connection internet available", "No connection");
            }
        }

        private void map_ZoomLevelChanged(MapControl sender, object args)
        {
            foreach (var element in map.Children.ToList())
            {
                if (element is StackPanel)
                {
                    map.Children.Remove(element);
                }
                if (currentCenterMap != null)
                {
                    if (element is UserPin)
                    {
                        (element as UserPin).SetRadius(currentCenterMap.Position.Latitude, sender.ZoomLevel);
                    }
                }
            }
        }

        //private async void map_CenterChanged(MapControl sender, object args)
        //{
        //    if (forceCenter)
        //    {
        //        await map.TrySetViewAsync(currentCenterMap);
        //    }
        //    //Debug.WriteLine("cambio centro -> centro");
        //    //centered = false;
        //}

        //    private void map_Tapped(object sender, TappedRoutedEventArgs e)
        //    {
        //        centered = false;
        //        Debug.WriteLine("cambio centro -> tap");
        //    }      

        //    private void map_MapDoubleTapped(MapControl sender, MapInputEventArgs args)
        //    {
        //        centered = false;
        //        Debug.WriteLine("cambio centro -> doppio");
        //    }

        //    private void map_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        //    {
        //        centered = false;
        //        Debug.WriteLine("cambio centro -> manipulation");
        //    }
        //}
    }
}

