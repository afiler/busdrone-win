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

namespace Busdrone
{
    public class Request
    {

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
        GeoCoordinateWatcher watcher;
        bool positionSet = false;

        public MainPage()
        {
            InitializeComponent();

            watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
            watcher.MovementThreshold = 20; // 20 meters
            watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(OnPositionChanged);
            watcher.Start();

            websocket = new WebSocket("ws://busdrone.com:28737/");
            websocket.MessageReceived += new EventHandler<MessageReceivedEventArgs>(OnMessage);
            websocket.Opened += new EventHandler(OnOpen);
            websocket.Open();
        }

        private void OnOpen(Object sender, EventArgs e)
        {
            Debug.WriteLine("Websocket open");
        }

        private void OnMessage(object sender, MessageReceivedEventArgs e)
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
            }
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
                    p.MouseLeftButtonUp += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e)=>
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

        void OnMarkerClick(Pushpin p, VehicleReport v)
        {
            Debug.WriteLine("OnMarkerClick: "+v.uid);
            //p.Width = 200;
            //p.Content = v.route + " " + v.destination;
        }

        void OnPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (!positionSet) {
                map.Center = e.Position.Location;
                positionSet = true;
            }
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
    }
}