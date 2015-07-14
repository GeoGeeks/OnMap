using Esri.ArcGISRuntime.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Foundation.Collections;
using System.Linq;

namespace Coll.Security
{
	/// <summary>
	/// Security class that defines global challenge method for accessing arcgis.com portal services
	/// </summary>
	internal class PortalSecurity
	{
		//Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        //Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

        //private const string PORTAL_URL = MainPage.GetUrl();//://www.arcgis.com/sharing/rest";

		// *** TODO: Replace CLIENT_ID with your arcgis.com App ID ***
        private const string CLIENT_ID = "bUv7kOSXYaOEt9fV";
		private const string REDIRECT_URI = "urn:ietf:wg:oauth:2.0:oob";

		// Challenge method should prompt for portal oauth username / password if necessary
		public static async Task<Credential> Challenge(CredentialRequestInfo arg)
		{
			// Register Portal Server if necessary
			var serverInfo = IdentityManager.Current.FindServerInfo(MainPage.GetUrl());
			if (IdentityManager.Current.Credentials.Count() == 0)// || */serverInfo == null)
			{
				serverInfo = new ServerInfo()
				{
                    ServerUri = MainPage.GetUrl(),
					TokenAuthenticationType = TokenAuthenticationType.OAuthAuthorizationCode,
					OAuthClientInfo = new OAuthClientInfo()
					{
						ClientId = CLIENT_ID,
						RedirectUri = REDIRECT_URI
					}
				};
				IdentityManager.Current.RegisterServer(serverInfo);
			}
            //if(IdentityManager.Current.Credentials.)
            return await IdentityManager.Current.GenerateCredentialAsync(MainPage.GetUrl());
		}
	}
}