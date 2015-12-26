using Parse;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using ReGroup.Common;
using ReGroup.Model;
using ReGroup;
using Windows.System.Display;
using Windows.UI.Xaml.Media.Animation;

// Il modello di applicazione vuota è documentato all'indirizzo http://go.microsoft.com/fwlink/?LinkId=234227

namespace ReGroup
{
    /// <summary>
    /// Fornisci un comportamento specifico dell'applicazione in supplemento alla classe Application predefinita.
    /// </summary>
    public sealed partial class App : Application
    {

        public static string FBClientID = "1665000427091560";
        public static string FBScope = "public_profile, email, user_friends";

        public static string ParseAppID = "hmayaW1keDwI17pq5sLBtgfj4zkGDxR6dF5oNeY2";
        public static string ParseDotNetKey = "8ZXwR8aNdDqhdoxuYaKjQWPVNe5pPzveKjmYQyHs";

        public static System.Collections.Generic.Dictionary<string, FBFriend> FriendsOnMap;

        DisplayRequest req;       

#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;
        public static ContinuationManager ContinuationManager { get; private set; }

#endif

        /// <summary>
        /// Inizializza l'oggetto Application singleton. Si tratta della prima riga del codice creato
        /// eseguita e, come tale, corrisponde all'equivalente logico di main() o WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;

            this.Resuming += App_Resuming;
            ParseClient.Initialize(ParseAppID, ParseDotNetKey);
            ParseFacebookUtils.Initialize(FBClientID);
#if WINDOWS_PHONE_APP
            ContinuationManager = new ContinuationManager();
#endif           
            req = new DisplayRequest();
            req.RequestActive();            
        }

        void App_Resuming(object sender, object e)
        {
            //Debug.WriteLine("Entrato nel metodo resume");
            req.RequestActive();            
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = false;
            }
#endif

            Windows.UI.Xaml.Controls.Frame rootFrame = Window.Current.Content as Windows.UI.Xaml.Controls.Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Windows.UI.Xaml.Controls.Frame();

                SuspensionManager.RegisterFrame(rootFrame, "appFrame");
                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    await SuspensionManager.RestoreAsync();                   
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
#endif

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            var rootFrame = sender as Windows.UI.Xaml.Controls.Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }
#endif

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();                       
            if (ParseUser.CurrentUser != null)
            {
                ParseUser.CurrentUser["shared"] = false;
                await ParseUser.CurrentUser.SaveAsync();
            }
            req.RequestRelease();          
            await SuspensionManager.SaveAsync();
            
#if WINDOWS_PHONE_APP
            ContinuationManager.MarkAsStale();
#endif
            deferral.Complete();
        }      

        protected override void OnActivated(IActivatedEventArgs args)
        {            
#if WINDOWS_PHONE_APP
            if (args.Kind == ActivationKind.WebAuthenticationBrokerContinuation)
            {
                var continuationEventArgs = args as IContinuationActivatedEventArgs;
                if (continuationEventArgs != null)
                {
                    ContinuationManager.Continue(continuationEventArgs);
                    ContinuationManager.MarkAsStale();
                }

            }
#endif
        }
    }
}