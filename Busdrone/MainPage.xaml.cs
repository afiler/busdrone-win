using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Controls.Maps;
using Microsoft.Phone.Shell;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Device.Location;
using System.Collections.ObjectModel;
using WebSocket4Net;
using Newtonsoft.Json;
using System.Threading;

namespace Busdrone
{
    public class Request
    {
        public String type;
        public String trip_uid;
        public double lat, lon, lat_delta, lon_delta;
        public double zoom;
    }

    public class VehicleReport
    {
        public String uid;
	    public String dataProvider;
	    public String vehicleOperator;
	    public String vehicleType;
	    public String vehicleId;
	    public String prevStop;
	    public String nextStop;
	    public String coach;
	    public String name;
	    public String routeId;
	    public String route;
	    public String tripId;
	    public String destination;
	    public String color;
	    public int speedMph;
	    public int speedKmh;
	    public double lat;
	    public double lon;
	    public double heading;
        public long timestamp;

        public String tripUid
        {
            get { return dataProvider + "/" + tripId; }
        }
    }

    public class Event
    {
        public String type;
        public VehicleReport[] vehicles;
        public VehicleReport vehicle;
        public String vehicle_uid;
        public String trip_uid;
        public String polyline;
    }

    public partial class MainPage : PhoneApplicationPage
    {
        WebSocket websocket;
        Dictionary<String, Pushpin> markers = new Dictionary<String, Pushpin>();
        Dictionary<String, MapPolyline> tripPolylines = new Dictionary<String, MapPolyline>();
        GeoCoordinateWatcher watcher;
        bool positionSet = false;
        String selectedTripUid;

        Timer reconnectTimer;
        Random rnd1 = new Random();

        public MainPage()
        {
            InitializeComponent();

            map.MouseLeftButtonUp += new MouseButtonEventHandler(OnMapClick);
            //map.MapPan += new EventHandler<MapDragEventArgs>(OnMapPan);
            //map.MapResolved += new EventHandler<MapDragEventArgs>(OnMapPan);
            //map.Tap += new EventHandler<GestureEventArgs>(OnMapClick);

            watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
            watcher.MovementThreshold = 20; // 20 meters
            watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(OnPositionChanged);
            watcher.Start();

            websocket = new WebSocket("ws://busdrone.com:28737/");
            websocket.MessageReceived += new EventHandler<MessageReceivedEventArgs>(OnWsMessage);
            websocket.Opened += new EventHandler(OnWsOpen);
            websocket.Closed += new EventHandler(OnWsClose);
            websocket.Open();

            // Check websocket is connected every 10-20 seconds
            TimerCallback onReconnectCallback = OnReconnectTimer;
            reconnectTimer = new Timer(onReconnectCallback, null, (10 + rnd1.Next(10)) * 1000, (10 + rnd1.Next(10)) * 1000);
        }

        private void OnWsOpen(Object sender, EventArgs e)
        {
            Debug.WriteLine("Websocket open");
        }

        private void OnWsMessage(Object sender, MessageReceivedEventArgs e)
        {
            var wsEvent = JsonConvert.DeserializeObject<Event>(e.Message);

            if (wsEvent.type == "init") {
                foreach (VehicleReport v in wsEvent.vehicles)
                    AddOrUpdateVehicle(v);
            } else if (wsEvent.type == "update_vehicle") {
                AddOrUpdateVehicle(wsEvent.vehicle);
            } else if (wsEvent.type == "remove_vehicle") {
                RemoveVehicle(wsEvent.vehicle_uid);
            }
            else if (wsEvent.type == "trip_polyline")
            {
                Debug.WriteLine("Message: " + wsEvent.type);
                AddPolyline(wsEvent.polyline, wsEvent.trip_uid);
            }
        }

        private void OnWsClose(Object sender, EventArgs e)
        {

        }

        private void OnReconnectTimer(Object stateInfo)
        {
            if (websocket.State == WebSocketState.Closed)
                websocket.Open();
        }

        private void AddOrUpdateVehicle(VehicleReport v)
        {
            if (markers.ContainsKey(v.uid)) {
                Dispatcher.BeginInvoke(() =>
                {
                    Pushpin p = markers[v.uid];
                    p.Location = new GeoCoordinate(v.lat, v.lon);
                });
            } else {
                Dispatcher.BeginInvoke(() =>
                {
                    Pushpin p = new Pushpin();
                    p.Background = new SolidColorBrush(ConvertStringToColor(v.color));
                    p.Content = v.route;
                    p.Location = new GeoCoordinate(v.lat, v.lon);
                    p.FontSize = 18;
                    p.Width = 50;
                    p.Height = 30;
                    p.PositionOrigin = PositionOrigin.Center;
                    //p.MouseLeftButtonUp += new MouseButtonEventHandler(OnMarkerClick);
                    /*p.MouseLeftButtonUp += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e)=>
                    {
                        OnMarkerClick(p, v);
                    });*/
                    p.Tap += new EventHandler<GestureEventArgs>((object sender, GestureEventArgs e) =>
                    {
                        OnMarkerClick(p, v);
                    });
                    //p.PositionOrigin = new PositionOrigin(2, 0);
                    map.Children.Add(p);

                    markers[v.uid] = p;
                });
            }
        }

        private void RemoveVehicle(String uid)
        {
            if (!markers.ContainsKey(uid)) return;
            var marker = markers[uid];
            Dispatcher.BeginInvoke(() =>
            {
                map.Children.Remove(marker);
            });
            markers.Remove(uid);
        }

        void OnMapPan(object sender, EventArgs e)
        {
            Request request = new Request
            {
                type = "location",
                lat = map.Center.Latitude,
                lon = map.Center.Longitude,
                zoom = map.ZoomLevel
            };

            String json = JsonConvert.SerializeObject(request);
            Debug.WriteLine("Request: " + json);
            websocket.Send(json);
        }

        void OnMapClick(object sender, MouseButtonEventArgs e)
        //void OnMapClick(object sender, GestureEventArgs e)
        {
            Debug.WriteLine("OnMapClick");
            infoPanel.Visibility = Visibility.Collapsed;
            routeText.Text = "";
            routeDescription.Text = "";
            ClearTripPolyline();
        }

        void OnMarkerClick(Pushpin p, VehicleReport v)
        {
            Debug.WriteLine("OnMarkerClick: "+v.uid);
            //p.Width = 200;
            //p.Content = v.route + " " + v.destination;
            routeText.Text = v.route;
            routeDescription.Text = v.destination;
            infoPanel.Visibility = Visibility.Visible;

            RequestTripPolyline(v.tripUid);
        }

        void OnPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (!positionSet) {
                map.Center = e.Position.Location;
                positionSet = true;
            }
        }

        void RequestTripPolyline(String tripUid)
        {
            Debug.WriteLine("RequestTripPolyline(" + tripUid + ")");

            ClearTripPolyline();
            selectedTripUid = tripUid;

            if (tripPolylines.ContainsKey(tripUid)) {
                ShowSelectedTripPolyline();
            } else {
                Request request = new Request
                {
                    type = "trip_polyline",
                    trip_uid = tripUid
                };

                String json = JsonConvert.SerializeObject(request);
                Debug.WriteLine("Request: " + json);
                websocket.Send(json);
            }
        }

        void ClearTripPolyline()
        {
            String tripUid = selectedTripUid;
            if (tripUid == null) return;
            
            map.Children.Remove(tripPolylines[tripUid]);
        }

        void AddPolyline(String encodedString, String tripUid) {
            Debug.WriteLine("AddPolyline for " + tripUid + ": "+encodedString);

            Dispatcher.BeginInvoke(() =>
            {
                MapPolyline polyline = new MapPolyline();
                polyline.Stroke = new System.Windows.Media.SolidColorBrush(Colors.Black);
                polyline.StrokeThickness = 5;
                polyline.Opacity = 0.5;
                polyline.Locations = DecodePolylineString(encodedString);
                tripPolylines[tripUid] = polyline;
            });
            ShowSelectedTripPolyline();
        }

        void ShowSelectedTripPolyline()
        {
            Dispatcher.BeginInvoke(() =>
            {
                String tripUid = selectedTripUid;
                MapPolyline polyline = tripPolylines[selectedTripUid];
                map.Children.Add(polyline);
            });
        }

        public Color ConvertStringToColor(String hex)
        {
            //remove the # at the front
            hex = hex.Replace("#", "");

            byte a = 255;
            byte r = 255;
            byte g = 255;
            byte b = 255;

            int start = 0;

            //handle ARGB strings (8 characters long)
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                start = 2;
            }

            //convert RGB characters to bytes
            r = byte.Parse(hex.Substring(start, 2), System.Globalization.NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(start + 2, 2), System.Globalization.NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(start + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }
  
        public static LocationCollection DecodePolylineString(string encodedPoints)
        {
            if (string.IsNullOrEmpty(encodedPoints))
                throw new ArgumentNullException("encodedPoints");

            LocationCollection locationCollection = new LocationCollection();

            char[] polylineChars = encodedPoints.ToCharArray();
            int index = 0;

            int currentLat = 0;
            int currentLng = 0;
            int next5bits;
            int sum;
            int shifter;

            while (index < polylineChars.Length)
            {
                // calculate next latitude
                sum = 0;
                shifter = 0;
                do
                {
                    next5bits = (int)polylineChars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length)
                    break;

                currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                //calculate next longitude
                sum = 0;
                shifter = 0;
                do
                {
                    next5bits = (int)polylineChars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length && next5bits >= 32)
                    break;

                currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                //yield return new GeoCoordinate(Convert.ToDouble(currentLat) / 1E5, Convert.ToDouble(currentLng) / 1E5);
                locationCollection.Add(new GeoCoordinate(Convert.ToDouble(currentLat) / 1E5, Convert.ToDouble(currentLng) / 1E5));
            }

            return locationCollection;
        }
    }
}