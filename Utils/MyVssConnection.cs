using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TFS.Utils
{
    public class MyVssConnection
    {
        private VssConnection vssConnection;

        public MyVssConnection(string domain, string login, string password)
        {
            var serverUrl = new Uri(GlobalSetting.URL);
            var clientCredentials = new VssCredentials(new WindowsCredential(new NetworkCredential(login, password, domain)));
            vssConnection = new VssConnection(serverUrl, clientCredentials);
        }

        public MyVssConnection(string token)
        {
            var creds = new VssBasicCredential(string.Empty, token);
            vssConnection = new VssConnection(new Uri(GlobalSetting.URL), creds);
        }

        public VssConnection GetConnection()
        {
            return vssConnection;
        }
    }
}
