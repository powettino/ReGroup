using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace ReGroup.utility
{
    public class ConnectionUtility
    {

        public static bool HasInternetAccess { get; private set; }

        public ConnectionUtility()
        {
            NetworkInformation.NetworkStatusChanged += NetworkInformationOnNetworkStatusChanged;
            CheckInternetAccess();
        }

        private void NetworkInformationOnNetworkStatusChanged(object sender)
        {
            CheckInternetAccess();
        }

        private void CheckInternetAccess()
        {            
            var connectionProfile = NetworkInformation.GetInternetConnectionProfile();
            HasInternetAccess = (NetworkInterface.GetIsNetworkAvailable() && connectionProfile != null &&
                                 connectionProfile.GetNetworkConnectivityLevel() ==
                                 NetworkConnectivityLevel.InternetAccess);
        }
    }
}
