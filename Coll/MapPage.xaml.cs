using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.Tasks.NetworkAnalyst;
using Esri.ArcGISRuntime.WebMap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.UI.Input;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Coll
{
    public sealed partial class MapPage : Windows.UI.Xaml.Controls.Page, IFileOpenPickerContinuable
    {
        private const string OnlineLocatorUrl = "http://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer";
        private WebMapViewModel _currentVM;
        private LocatorServiceInfo _locatorServiceInfo;
        private OnlineLocatorTask _locatorTask;

        private const string OnlineRoutingService = "http://route.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World";
        private OnlineRouteTask _routeTask;
        private Esri.ArcGISRuntime.Symbology.Symbol _directionPointSymbol;
        private GraphicsOverlay _stopsOverlay;
        private GraphicsOverlay _routesOverlay;
        private GraphicsOverlay _directionsOverlay;
        private GraphicsOverlay _myLocationOverlay;

        private FeatureLayer _layer;
        private ArcGISFeatureTable _table;
        
        private Dictionary<String,String> _campos;
        private Windows.Storage.StorageFile Foto;
        public MapPage()
        {
            this.InitializeComponent();

            MyMapView.LocationDisplay.LocationProvider = new SystemLocationProvider();
            MyMapView.LocationDisplay.LocationProvider.StartAsync();

            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            MyMapView.Loaded += MyMapView_Loaded;
            _locatorTask = new OnlineLocatorTask(new Uri(OnlineLocatorUrl));
            _locatorTask.AutoNormalize = true;

            _directionPointSymbol = LayoutRoot.Resources["directionPointSymbol"] as Esri.ArcGISRuntime.Symbology.Symbol;
            _stopsOverlay = MyMapView.GraphicsOverlays["StopsOverlay"];
            _routesOverlay = MyMapView.GraphicsOverlays["RoutesOverlay"];
            _directionsOverlay = MyMapView.GraphicsOverlays["DirectionsOverlay"];
            _myLocationOverlay = MyMapView.GraphicsOverlays["LocationOverlay"];
            _routeTask = new OnlineRouteTask(new Uri(OnlineRoutingService));

            _campos = new Dictionary<String, String>();
            if (PortalSearch.GetSelectedItem().Map.Layers.Count() > 0)
            {
                foreach (Layer i in PortalSearch.GetSelectedItem().Map.Layers)
                {
                    try
                    {
                        if ( !((FeatureLayer)i).FeatureTable.IsReadOnly && ((FeatureLayer)i).FeatureTable.GeometryType == GeometryType.Point)
                        {
                            _layer = i as FeatureLayer;
                            _table = (ArcGISFeatureTable)_layer.FeatureTable;
                            MenuFlyoutAddButton.IsEnabled = true;
                        }
                    }
                    catch
                    {

                    }

                }
            }
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            e.Handled = true;
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            this.Frame.Navigate(typeof(PortalSearch));
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private async void MyMapView_Loaded(object sender, RoutedEventArgs e)
        {
            var portal = await ArcGISPortal.CreateAsync();
            _currentVM = PortalSearch.GetSelectedItem(); 
            MyMapView.Map = _currentVM.Map;
            var result = await portal.ArcGISPortalInfo.SearchBasemapGalleryAsync();
            basemapList.ItemsSource = result.Results;
        }

        private async void basemapList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null || e.AddedItems.Count <= 0)
                return;

            try
            {
                BaseMapProgress.Visibility = Visibility.Visible;

                var item = e.AddedItems[0] as ArcGISPortalItem;
                var webmap = await WebMap.FromPortalItemAsync(item);
                var basemapVM = await WebMapViewModel.LoadAsync(webmap, _currentVM.ArcGISPortal);
                _currentVM.Basemap = basemapVM.Basemap;
            }
            catch (Exception ex)
            {
                var _ = new MessageDialog(ex.Message, "Sample Error").ShowAsync();
            }
            finally
            {
                BaseMapProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void MyMapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
        {
            if (e.LoadError == null)
                return;

            Debug.WriteLine(string.Format("Error while loading layer : {0} - {1}", e.Layer.ID, e.LoadError.Message));
        }

        private void LocationButton_Checked(object sender, RoutedEventArgs e)
        {
            _myLocationOverlay.Graphics.Clear();
            var point = MyMapView.LocationDisplay.CurrentLocation.Location;
            var buffer = GeometryEngine.GeodesicBuffer(point, 300, LinearUnits.Meters);
            MyMapView.SetView(buffer.Extent);

            var symbol = new PictureMarkerSymbol() { Width = 48, Height = 48, YOffset = 24 };
            symbol.SetSourceAsync(new Uri("ms-appx:///Assets/CollPin.png"));
            var graphic = new Graphic()
            {
                Geometry = point,
                Symbol = symbol
            };
            _myLocationOverlay.Graphics.Add(graphic);
        }

        private async void GeocodeButton_Click(object sender, RoutedEventArgs e)
        {
            GeocodeButton.IsEnabled = false;
            geocodeText.IsEnabled = false;
            try
			{
				GeoCodeProgress.Visibility = Visibility.Visible;

                if (_stopsOverlay.Graphics.Count() > 0)
                {
                    panelResults.Visibility = Visibility.Collapsed;
                    _stopsOverlay.Graphics.Clear();
                    _routesOverlay.Graphics.Clear();
                    MyMapView.Overlays.Items.Clear();
                    _directionsOverlay.GraphicsSource = null;
                }

				if (_locatorServiceInfo == null)
					_locatorServiceInfo = await _locatorTask.GetInfoAsync();

				var candidateResults = await _locatorTask.GeocodeAsync(
					GetInputAddressFromUI(), new List<string> { "Addr_type", "Score", "X", "Y" }, MyMapView.SpatialReference, CancellationToken.None);

				if (candidateResults == null || candidateResults.Count == 0)
					throw new Exception("No se encontró la dirección.");

                var found = false;
                foreach (var candidate in candidateResults)
                    if (candidate.Score >= 90)
                    {
                        AddGraphicFromLocatorCandidate(candidate);
                        found = true;
                        MyMapView.SetView(candidate.Extent.Expand(1));
                        await MyMapView.ZoomToScaleAsync(5000);
                        break;
                    }
                GeocodeFlyout.Hide();
                if (!found)
                    throw new Exception("No se encontró la dirección.");
			}
			catch (AggregateException ex)
			{
				var innermostExceptions = ex.Flatten().InnerExceptions;
				if (innermostExceptions != null && innermostExceptions.Count > 0)
				{
					var _x = new MessageDialog(string.Join(" > ", innermostExceptions.Select(i => i.Message).ToArray()), "Error").ShowAsync();
				}
				else
				{
					var _x = new MessageDialog(ex.Message, "Error").ShowAsync();
				}
			}
			catch (System.Exception ex)
			{
				var _x = new MessageDialog(ex.Message, "Error").ShowAsync();
			}
			finally
			{
				GeoCodeProgress.Visibility = Visibility.Collapsed;
                GeocodeButton.IsEnabled = true;
                geocodeText.IsEnabled = true;
			}
		}

        private async void mapView1_MapViewTapped(object sender, Esri.ArcGISRuntime.Controls.MapViewInputEventArgs e)
        {
            if (MyMapView.Editor.IsActive || _layer == null)
                return;
            _layer.ClearSelection();

            Graphic hitGraphic = await _stopsOverlay.HitTestAsync(MyMapView, e.Position);
            var features = await _layer.HitTestAsync(MyMapView, e.Position);
            
            if (hitGraphic != null)
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
                return;
            }

            if (features != null)
            {
                string message = null;

                MyMapView.Overlays.Items.Clear();
                try
                {
                    if (!features.Any())
                        return;

                    
                    var featureID = features.FirstOrDefault();
                    _layer.SelectFeatures(new long[] { featureID });
                    var feature = (GeodatabaseFeature)await _layer.FeatureTable.QueryAsync(featureID);

                    message = _layer.DisplayName;
                    for (int i = 0; i < feature.Attributes.Values.Count(); i++ )
                    {
                        if (feature.Attributes.Keys.ToArray()[i].ToString() != "OBJECTID" )
                            message += "\n" + feature.Attributes.Keys.ToArray()[i]+ ": " + feature.Attributes.Values.ToArray()[i];
                    }
                }
                catch (Exception ex)
                {  
                    message = ex.Message;
                }
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var result = message;
                    var overlay = new ContentControl() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                    overlay.Template = LayoutRoot.Resources["PopUpTipTemplate"] as ControlTemplate;
                    overlay.DataContext = result;
                    MapView.SetViewOverlayAnchor(overlay, e.Location);
                    MyMapView.Overlays.Items.Add(overlay);
                }              
            }
        }



        private void AddGraphicFromLocatorCandidate(LocatorGeocodeResult candidate)
        {

            if ((bool)LocationButton.IsChecked)
            {
                MenuFlyoutGotoButton.IsEnabled = true;
            }
            if (_stopsOverlay.Graphics.Count() > 0)
            {
                panelResults.Visibility = Visibility.Collapsed;
                _stopsOverlay.Graphics.Clear();
                _routesOverlay.Graphics.Clear();
                MyMapView.Overlays.Items.Clear();
                _directionsOverlay.GraphicsSource = null;
            }
            _stopsOverlay.Graphics.Add(CreateStopGraphic(MyMapView.LocationDisplay.CurrentLocation.Location, _stopsOverlay.Graphics.Count + 1));
            _stopsOverlay.Graphics.Add(CreateStopGraphic(new MapPoint(candidate.Location.X, candidate.Location.Y, MyMapView.SpatialReference), _stopsOverlay.Graphics.Count + 1));
        }

        private IDictionary<string, string> GetInputAddressFromUI()
        {
            Dictionary<string, string> address = new Dictionary<string, string>();
            address[_locatorServiceInfo.SingleLineAddressField.FieldName] = geocodeText.Text;
            return address;
        }

        private void GeocodeCanelButton_Click(object sender, RoutedEventArgs e)
        {
            GeocodeFlyout.Hide();
        }

        // Calculate the route
        private async void MyMapView_MapViewHolding(object sender, Esri.ArcGISRuntime.Controls.MapViewInputEventArgs e)
        {
            if ((bool)LocationButton.IsChecked)
            {
                MenuFlyoutGotoButton.IsEnabled = true;
            }
            e.Handled = true;

            if (_stopsOverlay.Graphics.Count() > 0)
            {
                panelResults.Visibility = Visibility.Collapsed;
                _stopsOverlay.Graphics.Clear();
                _routesOverlay.Graphics.Clear();
                MyMapView.Overlays.Items.Clear();
                _directionsOverlay.GraphicsSource = null;
            }
            _stopsOverlay.Graphics.Add(CreateStopGraphic(MyMapView.LocationDisplay.CurrentLocation.Location, _stopsOverlay.Graphics.Count + 1));
            _stopsOverlay.Graphics.Add(CreateStopGraphic(e.Location, _stopsOverlay.Graphics.Count + 1));

            try
            {
                var result = await _locatorTask.ReverseGeocodeAsync(e.Location, 50, SpatialReferences.Wgs84, CancellationToken.None);

                var overlay = new ContentControl() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                overlay.Template = LayoutRoot.Resources["MapTipTemplate"] as ControlTemplate;
                overlay.DataContext = result;
                MapView.SetViewOverlayAnchor(overlay, e.Location);
                MyMapView.Overlays.Items.Add(overlay);
            }
            catch (AggregateException)
			{
				//var _x = new MessageDialog(aex.InnerExceptions[0].Message, "Reverse Geocode").ShowAsync();
                return;
			}
			catch (Exception)
			{
				//var _x = new MessageDialog(ex.Message, "Reverse Geocode").ShowAsync();
                return;
			}
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private Graphic CreateStopGraphic(MapPoint location, int id)
        {
            var symbol = new PictureMarkerSymbol() { Width = 48, Height = 48, YOffset = 24 };
            symbol.SetSourceAsync(new Uri("ms-appx:///Assets/CollPin.png"));

            if (_stopsOverlay.Graphics.Count() < 1)
            {
                var graphic = new Graphic()
                    {
                        Geometry = location,
                    };
                return graphic;
            }
            else
            {
                var graphic = new Graphic()
                {
                    Geometry = location,
                    Symbol = symbol
                };
                return graphic;
            }            
        }

        private Graphic GraphicFromRouteDirection(RouteDirection rd)
        {
            var graphic = new Graphic(rd.Geometry);
            graphic.Attributes.Add("Direction", rd.Text);
            graphic.Attributes.Add("Time", string.Format("{0:h\\:mm\\:ss}", rd.Time));
            graphic.Attributes.Add("Length", string.Format("{0:0.00}", rd.GetLength(LinearUnits.Kilometers)));
            if (rd.Geometry is MapPoint)
                graphic.Symbol = _directionPointSymbol;

            return graphic;
        }

        private async void listDirections_SelectionChanged(object sender, Windows.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            _directionsOverlay.ClearSelection();

            if (e.AddedItems != null && e.AddedItems.Count == 1)
            {
                var graphic = e.AddedItems[0] as Graphic;
                ShowHideDirectionsButton.Icon = new SymbolIcon(Windows.UI.Xaml.Controls.Symbol.Add);
                listDirections.Visibility = Visibility.Collapsed;
                await MyMapView.SetViewAsync(graphic.Geometry.Extent.Expand(1.25));
                graphic.IsSelected = true;
            }
        }

        private void ShowHideDirectionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (listDirections.Visibility == Visibility.Collapsed)
            {
                ShowHideDirectionsButton.Icon = new SymbolIcon(Windows.UI.Xaml.Controls.Symbol.Remove);
                listDirections.Visibility = Visibility.Visible;
            }
            else
            {
                ShowHideDirectionsButton.Icon = new SymbolIcon(Windows.UI.Xaml.Controls.Symbol.Add);
                listDirections.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseDirectionsButton_Click(object sender, RoutedEventArgs e)
        {
            panelResults.Visibility = Visibility.Collapsed;
            _stopsOverlay.Graphics.Clear();
            _routesOverlay.Graphics.Clear();
            MyMapView.Overlays.Items.Clear();
            _directionsOverlay.GraphicsSource = null;
        }

        private async void MenuFlyoutGotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                panelResults.Visibility = Visibility.Visible;
                progress.Visibility = Visibility.Visible;

                RouteParameters routeParams = await _routeTask.GetDefaultParametersAsync();
                routeParams.OutSpatialReference = MyMapView.SpatialReference;
                routeParams.ReturnDirections = true;
                routeParams.DirectionsLengthUnit = LinearUnits.Kilometers;
                routeParams.DirectionsLanguage = new CultureInfo("es"); // CultureInfo.CurrentCulture;
                routeParams.SetStops(_stopsOverlay.Graphics);

                var routeResult = await _routeTask.SolveAsync(routeParams);
                if (routeResult == null || routeResult.Routes == null || routeResult.Routes.Count() == 0)
                    throw new Exception("No se pude calcular la ruta");

                var route = routeResult.Routes.First();
                _routesOverlay.Graphics.Add(new Graphic(route.RouteFeature.Geometry));

                _directionsOverlay.GraphicsSource = route.RouteDirections.Select(rd => GraphicFromRouteDirection(rd));

                var totalTime = route.RouteDirections.Select(rd => rd.Time).Aggregate(TimeSpan.Zero, (p, v) => p.Add(v));
                var totalLength = route.RouteDirections.Select(rd => rd.GetLength(LinearUnits.Kilometers)).Sum();
                txtRouteTotals.Text = string.Format("Tiempo: {0:h':'mm':'ss} / Distancia: {1:0.00} km", totalTime, totalLength);

                await MyMapView.SetViewAsync(route.RouteFeature.Geometry.Extent.Expand(1.80));


            }
            catch (AggregateException ex)
            {
                var message = ex.Message;
                var innermostExceptions = ex.Flatten().InnerExceptions;
                if (innermostExceptions != null && innermostExceptions.Count > 0)
                    message = innermostExceptions[0].Message;

                var _x = new MessageDialog(message, "Error").ShowAsync();
            }
            catch (Exception ex)
            {
                var _x = new MessageDialog(ex.Message, "Error").ShowAsync();
            }
            finally
            {
                progress.Visibility = Visibility.Collapsed;
                if (_directionsOverlay.Graphics.Count() > 0)
                {
                    panelResults.Visibility = Visibility.Visible;
                }
            }
        }

        private void LocationButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _myLocationOverlay.Graphics.Clear();
            MenuFlyoutGotoButton.IsEnabled = false;
        }

        private void ClearGeocodeTextButton_Click(object sender, RoutedEventArgs e)
        {
            geocodeText.Text = "";

        }

        private void MenuFlyoutAddButton_Click(object sender, RoutedEventArgs e)
        {
            displayImagePreview.Source = null;
            var feature1 = _layer.FeatureTable.Schema.Fields.Where(u => u.Name != "OBJECTID").ToList();
            AttributesListView.ItemsSource = feature1;
            _campos.Clear();
            NewPointFlyout.Visibility = Visibility.Visible;
            panelResults.Visibility = Visibility.Collapsed;            
        }

        private async void NewPointFlyoutButton_Click(object sender, RoutedEventArgs e)
        {
            NewPointFlyout.Visibility = Visibility.Collapsed;
            string message = null;
            try
            {
                var mapPoint = _stopsOverlay.Graphics.Last().Geometry;

                if (_table.ServiceInfo.Types == null)
                    return;
                
                
                var feature = new GeodatabaseFeature(_table.Schema) { Geometry = mapPoint };
                if (_table.ServiceInfo.Types == null)
                    return;

                var template = _table.ServiceInfo.Templates.FirstOrDefault();
                if (template == null || template.Prototype == null || template.Prototype.Attributes == null)
                    return;
                foreach (var item in template.Prototype.Attributes)
                {
                    string value = "";
                    if (_campos.TryGetValue(item.Key.ToString(), out value))
                    {
                        try
                        {
                            feature.Attributes[item.Key] = value;
                        }
                        catch (Exception)
                        {
                            switch(_table.Schema.Fields.Where(u => u.Name == item.Key.ToString()).FirstOrDefault().Type){
                                case Esri.ArcGISRuntime.Data.FieldType.Integer:                                    
                                    feature.Attributes[item.Key] = Convert.ToInt32(value);
                                    break;
                                case Esri.ArcGISRuntime.Data.FieldType.Double:
                                    feature.Attributes[item.Key] = Convert.ToDouble(value);
                                    break;
                                case Esri.ArcGISRuntime.Data.FieldType.Date:
                                    feature.Attributes[item.Key] = Convert.ToDateTime(value);
                                    break;
                                default:
                                    throw;
                            }
                        }
                    }
                        
                }

                if (_table.CanAddFeature(feature))
                {
                    await _table.AddAsync(feature);
                    if (Foto != null && _table.CanAddAttachment(feature))
                    {
                        var featureID = ((ArcGISFeatureTable)_table).GetAddedFeatureIDs().FirstOrDefault();
                        await _table.AddAttachmentAsync(featureID, Foto);
                        
                    }
                    ((ServiceFeatureTable)_table).OutFields = new Esri.ArcGISRuntime.Tasks.Query.OutFields(feature.Attributes.Keys);
                    await ((ServiceFeatureTable)_table).ApplyEditsAsync();

                    if (Foto != null)
                    {
                        await ((ServiceFeatureTable)_table).ApplyAttachmentEditsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (_stopsOverlay.Graphics.Count() > 0)
            {
                
                _stopsOverlay.Graphics.Clear();
                _routesOverlay.Graphics.Clear();
                MyMapView.Overlays.Items.Clear();
                _directionsOverlay.GraphicsSource = null;
            }
            if (!string.IsNullOrWhiteSpace(message))
                await new MessageDialog(message).ShowAsync();
        }

        private void AttributesListViewTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_campos.ContainsKey(((TextBox)sender).Header.ToString()))
                _campos.Remove(((TextBox)sender).Header.ToString());
            _campos.Add(((TextBox)sender).Header.ToString(), ((TextBox)sender).Text);
        }

        private void NewPointCancelButton_Click(object sender, RoutedEventArgs e)
        {
            NewPointFlyout.Visibility = Visibility.Collapsed;
            if (_directionsOverlay.Graphics.Count() > 0)
            {
                panelResults.Visibility = Visibility.Visible;
            }
        }

        private void AddAttachButton_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.Pickers.FileOpenPicker openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;

            // Filter to include a sample subset of file types.
            openPicker.FileTypeFilter.Clear();
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".jpg");

            // Open the file picker.
            openPicker.PickSingleFileAndContinue();
        }
        public async void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            if (args.Files.Count > 0)
            {
                Foto = args.Files[0];
                using (IRandomAccessStream fileStream = await args.Files[0].OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    // Set the image source to the selected bitmap 
                    BitmapImage bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(fileStream);
                    displayImagePreview.Source = bitmapImage;
                } 
            }
            else
            {
                
            }
        }

    }
}
