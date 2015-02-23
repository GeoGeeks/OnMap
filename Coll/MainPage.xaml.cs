using Coll.Security;
using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace Coll
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static string url;
        public static MainPage Current;

        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

        public MainPage()
        {
            this.InitializeComponent();

            Current = this;

            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            localSettings.Values["https://www.arcgis.com"] = "https://www.arcgis.com";

            UrlComboBox.ItemsSource = localSettings.Values.Values.ToArray();
            UrlComboBox.SelectedItem = localSettings.Values.Values.First();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
        
        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            e.Handled = false;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.
            ResetVisibility();
            //this.InitializeComponent();
            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.
        }

        private async void AddUrlButton_Click(object sender, RoutedEventArgs e)
        {
            localSettings.Values[NewUrlTextBox.Text.ToString()] = NewUrlTextBox.Text.ToString();
            UrlComboBox.ItemsSource = localSettings.Values.Values.ToArray();
            AddUrlFlyout.Hide();

            var messageDialog = new MessageDialog("Se ha agregado la URL: "+NewUrlTextBox.Text.ToString());
            await messageDialog.ShowAsync();
        }

        private async void RemoveUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var messageDialog = new MessageDialog("¿Borrar URL?.");
            messageDialog.Commands.Add(new UICommand("Si", new UICommandInvokedHandler(this.CommandInvokedHandler)));
            messageDialog.Commands.Add(new UICommand("No"));
            await messageDialog.ShowAsync();
        }

        private void CommandInvokedHandler(IUICommand command)
        {
            localSettings.Values.Remove(UrlComboBox.SelectedItem.ToString());
            UrlComboBox.ItemsSource = localSettings.Values.Values.ToArray();
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UrlComboBox.IsEnabled = false;
                Start.Visibility = Visibility.Collapsed;
                LoadingRing.Visibility = Visibility.Visible;
                //LoadingRing.IsActive = true;
                MainBottomAppBar.Visibility = Visibility.Collapsed;
                url = UrlComboBox.SelectedItem.ToString();
                IdentityManager.Current.OAuthAuthorizeHandler = new OAuthAuthorizeHandler();
                IdentityManager.Current.ChallengeHandler = new ChallengeHandler(PortalSecurity.Challenge);
                var cred = await IdentityManager.Current.ChallengeHandler.CreateCredentialAsync(new CredentialRequestInfo());
                IdentityManager.Current.AddCredential(cred);
                if (IdentityManager.Current.Credentials.Count() > 0)
                {
                    HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
                    this.Frame.Navigate(typeof(PortalSearch));
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                ResetVisibility();
                var _x = new MessageDialog("No se ha iniciado sesión.").ShowAsync();
            }
        }

        public static string GetUrl()
        {
            return url+"/sharing/rest";
        }

        private void CancelAddUrl_Click(object sender, RoutedEventArgs e)
        {
            AddUrlFlyout.Hide();
        }

        internal static string GetUrlToken()
        {
            return url + "/sharing/generateToken";
        }

        private void ResetVisibility()
        {
            UrlComboBox.IsEnabled = true;
            Start.Visibility = Visibility.Visible;
            LoadingRing.Visibility = Visibility.Collapsed;
            //LoadingRing.IsActive = false;
            MainBottomAppBar.Visibility = Visibility.Visible;
        }

        private async void RateUsButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store:reviewapp?appid=" + Windows.ApplicationModel.Package.Current.Id.Name));
        }
    }
}
