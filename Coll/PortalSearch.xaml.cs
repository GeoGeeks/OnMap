using Coll;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.WebMap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Phone.UI.Input;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Coll
{
    /// <summary>
    /// 
    /// </summary>
    public partial class PortalSearch : Page
    {
        private ArcGISPortal _portal;

        /// <summary>Construct Portal Search sample control</summary>
        private static WebMapViewModel SelectedViewModel;
        public PortalSearch()
        {
            InitializeComponent();
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            IdentityManager.Current.RegisterServer(
                new ServerInfo()
                {
                    ServerUri = MainPage.GetUrl(),
                    TokenServiceUri = MainPage.GetUrlToken(),
                });


            Loaded += control_Loaded;
        }
        
        private async void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            e.Handled= true;
            var messageDialog = new MessageDialog(_portal.CurrentUser.FullName.ToString() + " ¿Cerrar Sesión?");
            messageDialog.Commands.Add(new UICommand("Si", new UICommandInvokedHandler(this.CommandInvokedHandler)));
            messageDialog.Commands.Add(new UICommand("No"));
            await messageDialog.ShowAsync();
        }

        public static WebMapViewModel GetSelectedItem()
        {
            return SelectedViewModel;
        }

        private Task<Credential> Challenge(CredentialRequestInfo arg)
        {
            return Task.FromResult<Credential>(null);
        }

        private void control_Loaded(object sender, RoutedEventArgs e)
        {
            DoSearch("misMapas");
        }

        private void QueryText_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                DoSearch("buscar");
                e.Handled = true;
            }
        }

        private void DoSearch_Click(object sender, RoutedEventArgs e)
        {
            DoSearch("buscar");
            SearchFlyout.Hide();
        }

        private void ResetVisibility()
        {
            LoadingRing.Visibility = Visibility.Collapsed;
            PortalSearchBottomBar.Visibility = Visibility.Visible;
            ResultsListBox.IsEnabled = true;
        }

        // Search arcgis.com for web maps matching the query text
        private async void DoSearch(String boton)
        {
            _portal = await ArcGISPortal.CreateAsync(new Uri(MainPage.GetUrl()));
            try
            {
                MyMapsProgressBar.Visibility = Visibility.Visible;
                ResultsListBox.ItemsSource = null;
                ResetVisibility();
                
                if (boton.Equals("buscar"))
                {
                    if (QueryText == null || string.IsNullOrEmpty(QueryText.Text.Trim()))
                        return;

                    var queryString = string.Format("{0} type:(\"web map\" NOT \"web mapping application\")", QueryText.Text.Trim());
                    if (_portal.CurrentUser != null && _portal.ArcGISPortalInfo != null && !string.IsNullOrEmpty(_portal.ArcGISPortalInfo.Id))
                        queryString = string.Format("{0} orgid:(\"{1}\")", queryString, _portal.ArcGISPortalInfo.Id);

                    var searchParameters = new SearchParameters()
                    {
                        QueryString = queryString,
                        SortField = "avgrating",
                        SortOrder = QuerySortOrder.Descending,
                        Limit = 20
                    };
                    var result = await _portal.SearchItemsAsync(searchParameters);
                    ResultsListBox.ItemsSource = result.Results;

                    if (result.Results != null && result.Results.Count() > 0)
                        ResultsListBox.SelectedIndex = 0;
                }
                else
                {
                    var result = await _portal.CurrentUser.GetItemsAsync();
                    foreach (ArcGISPortalItem i in result)
                    {
                        if (i.TypeName.Equals("Web Map"))
                        {
                            ResultsListBox.Items.Add(i); 
                        }
                    }
                    if (result != null && result.Count() > 0 && ResultsListBox.Items.Count() > 0)
                        ResultsListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                var _x = new MessageDialog(ex.Message, "Error").ShowAsync();
            }
            finally
            {
                MyMapsProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PortalSearchBottomBar.Visibility = Visibility.Collapsed;
                ResultsListBox.IsEnabled = false;
                LoadingRing.Visibility = Visibility.Visible;
                var item = ((Button)sender).DataContext as ArcGISPortalItem;
                var webmap = await WebMap.FromPortalItemAsync(item);
                SelectedViewModel = await WebMapViewModel.LoadAsync(webmap, _portal);
                HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
                this.Frame.Navigate(typeof(MapPage));
            }
            catch (Exception ex)
            {
                var _x = new MessageDialog(ex.Message, "Error").ShowAsync();
                ResetVisibility();
            }
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            ResultsListBox.ItemsSource = null;
            ResultsListBox.Items.Clear();
            
            try
            {
                IdentityManager.Current.FindCredential(MainPage.GetUrl());
                ResetVisibility();
                DoSearch("MisMapas");
            }
            catch
            {
                var _x = new MessageDialog("No se puden cargar los datos.").ShowAsync();
            }
        }

        private void SearchCancelButton_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();
        }

        private async void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            var messageDialog = new MessageDialog(_portal.CurrentUser.FullName.ToString() + " ¿Cerrar Sesión?");
            messageDialog.Commands.Add(new UICommand("Si", new UICommandInvokedHandler(this.CommandInvokedHandler)));
            messageDialog.Commands.Add(new UICommand("No"));
            await messageDialog.ShowAsync();
        }
        private void CommandInvokedHandler(IUICommand command)
        {
            for (int i = 0; i < IdentityManager.Current.Credentials.Count(); i++ )
                IdentityManager.Current.RemoveCredential(IdentityManager.Current.FindCredential(MainPage.GetUrl()));
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            this.Frame.Navigate(typeof(MainPage));
        }
    }

    // Helper class to get a description string from HTML content
    internal class HtmlToTextConverter : IValueConverter
    {
        private static string htmlLineBreakRegex = @"(<br */>)|(\[br */\])"; //@"<br(.)*?>";	// Regular expression to strip HTML line break tag
        private static string htmlStripperRegex = @"<(.|\n)*?>";	// Regular expression to strip HTML tags

        public static string GetHtmlToInlines(DependencyObject obj)
        {
            return (string)obj.GetValue(HtmlToInlinesProperty);
        }

        public static void SetHtmlToInlines(DependencyObject obj, string value)
        {
            obj.SetValue(HtmlToInlinesProperty, value);
        }

        // Using a DependencyProperty as the backing store for HtmlToInlinesProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HtmlToInlinesProperty =
          DependencyProperty.RegisterAttached("HtmlToInlines", typeof(string), typeof(HtmlToTextConverter), new PropertyMetadata(null, OnHtmlToInlinesPropertyChanged));

        private static void OnHtmlToInlinesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Paragraph)
            {
                if (e.NewValue == null)
                    (d as Paragraph).Inlines.Clear();
                else
                {
                    var splits = Regex.Split(e.NewValue as string, htmlLineBreakRegex, RegexOptions.IgnoreCase | RegexOptions.ECMAScript);
                    foreach (var line in splits)
                    {
                        string text = Regex.Replace(line, htmlStripperRegex, string.Empty);
                        Regex regex = new Regex(@"[ ]{2,}", RegexOptions.None);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            text = regex.Replace(text, @" "); //Remove multiple spaces
                            text = text.Replace("&quot;", "\""); //Unencode quotes
                            text = text.Replace("&nbsp;", " "); //Unencode spaces
                            (d as Paragraph).Inlines.Add(new Run() { Text = text });
                            (d as Paragraph).Inlines.Add(new LineBreak());
                        }
                    }
                }
            }
        }

        private static string ToStrippedHtmlText(object input)
        {
            string retVal = string.Empty;

            if (input != null)
            {
                // Replace HTML line break tags with $LINEBREAK$:
                retVal = Regex.Replace(input as string, htmlLineBreakRegex, "", RegexOptions.IgnoreCase);
                // Remove the rest of HTML tags:
                retVal = Regex.Replace(retVal, htmlStripperRegex, string.Empty);
                //retVal.Replace("$LINEBREAK$", "\n");
                retVal = retVal.Trim();
            }

            return retVal;
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string)
                return ToStrippedHtmlText(value);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    internal class ConcatenateStringsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var strings = value as IEnumerable<string>;
            if (strings == null)
                return value;

            return string.Join(", ", strings.ToArray());
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
