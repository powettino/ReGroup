using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls;

namespace ReGroup.Model
{
    public class FBFriend
    {
        private Uri pictureSquare;
        //private Uri pictureMap;
        private string id;
        private string name;
        private Geopoint geo;        

        public FBFriend(string friendName, string friendId, Uri picture = null)
        {
            Name = friendName;
            Picture = picture;
            Id = friendId;
            geo = null;
        }

        public override string ToString()
        {
            return Name + " " + Id;
        }

        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        public string Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
                pictureSquare = new Uri(string.Format("https://graph.facebook.com/{0}/picture?type=square", id));             
            }
        }

        public Uri Picture
        {
            get
            {
                return pictureSquare;
            }
            set
            {
                pictureSquare = value;
            }
        }

        public Geopoint Geopoint
        {
            get
            {
                return geo;
            }
            set
            {
                geo = value;
            }
        }     
    }
}
