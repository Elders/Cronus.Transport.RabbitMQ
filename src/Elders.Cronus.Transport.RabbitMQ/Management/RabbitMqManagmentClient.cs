using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Management.Model;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ.Management
{
    public class RabbitMqManagementClient
    {
        private readonly string hostUrl;
        private readonly string username;
        private readonly string password;
        private readonly int portNumber;
        private readonly JsonSerializerSettings Settings;

        private readonly bool runningOnMono;
        private readonly Action<HttpWebRequest> configureRequest;
        private readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(20);
        private readonly TimeSpan timeout;

        public RabbitMqManagementClient(
                string hostUrl,
                string username,
                string password,
                int portNumber = 15672,
                bool runningOnMono = false,
                TimeSpan? timeout = null,
                Action<HttpWebRequest> configureRequest = null,
                bool ssl = false)
        {
            var urlRegex = new Regex(@"^(http|https):\/\/.+\w$");
            Uri urlUri = null;
            if (string.IsNullOrEmpty(hostUrl))
            {
                throw new ArgumentException("hostUrl is null or empty");
            }

            if (hostUrl.StartsWith("https://"))
                ssl = true;

            if (ssl)
            {
                if (hostUrl.Contains("http://"))
                    throw new ArgumentException("hostUrl is illegal");
                hostUrl = hostUrl.Contains("https://") ? hostUrl : "https://" + hostUrl;
            }
            else
            {
                if (hostUrl.Contains("https://"))
                    throw new ArgumentException("hostUrl is illegal");
                hostUrl = hostUrl.Contains("http://") ? hostUrl : "http://" + hostUrl;
            }
            if (!urlRegex.IsMatch(hostUrl) || !Uri.TryCreate(hostUrl, UriKind.Absolute, out urlUri))
            {
                throw new ArgumentException("hostUrl is illegal");
            }
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("username is null or empty");
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("password is null or empty");
            }
            if (configureRequest == null)
            {
                configureRequest = x => { };
            }
            this.hostUrl = hostUrl;
            this.username = username;
            this.password = password;
            this.portNumber = portNumber;
            this.timeout = timeout ?? defaultTimeout;
            this.runningOnMono = runningOnMono;
            this.configureRequest = configureRequest;

            if (!runningOnMono)
            {
                LeaveDotsAndSlashesEscaped(ssl);
            }

            Settings = new JsonSerializerSettings
            {
                ContractResolver = new RabbitContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
            };
        }

        public Vhost CreateVirtualHost(string virtualHostName)
        {
            if (string.IsNullOrEmpty(virtualHostName))
            {
                throw new ArgumentException("virtualHostName is null or empty");
            }

            Put(string.Format("vhosts/{0}", virtualHostName));

            return GetVhost(virtualHostName);
        }

        public Vhost GetVhost(string vhostName)
        {
            return Get<Vhost>(string.Format("vhosts/{0}", SanitiseVhostName(vhostName)));
        }

        public IEnumerable<Vhost> GetVHosts()
        {
            return Get<IEnumerable<Vhost>>("vhosts");
        }

        public void CreatePermission(PermissionInfo permissionInfo)
        {
            if (permissionInfo == null)
            {
                throw new ArgumentNullException("permissionInfo");
            }

            Put(string.Format("permissions/{0}/{1}",
                   SanitiseVhostName(permissionInfo.GetVirtualHostName()),
                    permissionInfo.GetUserName()),
                permissionInfo);
        }

        public IEnumerable<User> GetUsers()
        {
            return Get<IEnumerable<User>>("users");
        }

        public User GetUser(string userName)
        {
            return Get<User>(string.Format("users/{0}", userName));
        }

        public User CreateUser(UserInfo userInfo)
        {
            if (userInfo == null)
            {
                throw new ArgumentNullException("userInfo");
            }

            Put(string.Format("users/{0}", userInfo.GetName()), userInfo);

            return GetUser(userInfo.GetName());
        }


        private void Put(string path)
        {
            var request = CreateRequestForPath(path);
            request.Method = "PUT";
            request.ContentType = "application/json";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                // The "Cowboy" server in 3.7.0's Management Client returns 201 Created. 
                // "MochiWeb/1.1 WebMachine/1.10.0 (never breaks eye contact)" in 3.6.1 and previous return 204 No Content
                // Also acceptable for a PUT response is 200 OK
                // See also http://stackoverflow.com/questions/797834/should-a-restful-put-operation-return-something
                if (!(response.StatusCode == HttpStatusCode.OK ||
                      response.StatusCode == HttpStatusCode.Created ||
                      response.StatusCode == HttpStatusCode.NoContent))
                {
                    throw new UnexpectedHttpStatusCodeException(response.StatusCode);
                }
            }
        }

        private void Put<T>(string path, T item)
        {
            var request = CreateRequestForPath(path);
            request.Method = "PUT";

            InsertRequestBody(request, item);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                // The "Cowboy" server in 3.7.0's Management Client returns 201 Created. 
                // "MochiWeb/1.1 WebMachine/1.10.0 (never breaks eye contact)" in 3.6.1 and previous return 204 No Content
                // Also acceptable for a PUT response is 200 OK
                // See also http://stackoverflow.com/questions/797834/should-a-restful-put-operation-return-something
                if (!(response.StatusCode == HttpStatusCode.OK ||
                      response.StatusCode == HttpStatusCode.Created ||
                      response.StatusCode == HttpStatusCode.NoContent))
                {
                    throw new UnexpectedHttpStatusCodeException(response.StatusCode);
                }
            }
        }

        private T Get<T>(string path, params object[] queryObjects)
        {
            var request = CreateRequestForPath(path, queryObjects);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new UnexpectedHttpStatusCodeException(response.StatusCode);
                }
                return DeserializeResponse<T>(response);
            }
        }

        private void InsertRequestBody<T>(HttpWebRequest request, T item)
        {
            request.ContentType = "application/json";

            var body = JsonConvert.SerializeObject(item, Settings);
            using (var requestStream = request.GetRequestStream())
            using (var writer = new StreamWriter(requestStream))
            {
                writer.Write(body);
            }
        }

        private void LeaveDotsAndSlashesEscaped(bool useSsl)
        {
            var getSyntaxMethod =
                typeof(UriParser).GetMethod("GetSyntax", BindingFlags.Static | BindingFlags.NonPublic);
            if (getSyntaxMethod == null)
            {
                throw new MissingMethodException("UriParser", "GetSyntax");
            }

            var uriParser = getSyntaxMethod.Invoke(null, new object[] { useSsl ? "https" : "http" });

            var setUpdatableFlagsMethod =
                uriParser.GetType().GetMethod("SetUpdatableFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setUpdatableFlagsMethod == null)
            {
                throw new MissingMethodException("UriParser", "SetUpdatableFlags");
            }

            setUpdatableFlagsMethod.Invoke(uriParser, new object[] { 0 });
        }

        private string SanitiseVhostName(string vhostName)
        {
            return vhostName.Replace("/", "%2f");
        }

        private T DeserializeResponse<T>(HttpWebResponse response)
        {
            var responseBody = GetBodyFromResponse(response);
            return JsonConvert.DeserializeObject<T>(responseBody);
        }

        private static string GetBodyFromResponse(HttpWebResponse response)
        {
            string responseBody;
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                {
                    //throw new EasyNetQManagementException("Response stream was null");
                }
                using (var reader = new StreamReader(responseStream))
                {
                    responseBody = reader.ReadToEnd();
                }
            }
            return responseBody;
        }

        private HttpWebRequest CreateRequestForPath(string path, object[] queryObjects = null)
        {
            var endpointAddress = BuildEndpointAddress(path);
            var queryString = BuildQueryString(queryObjects);

            var uri = new Uri(endpointAddress + queryString);

            if (runningOnMono)
            {
                // unsightly hack to fix path. 
                // The default vHost in RabbitMQ is named '/' which causes all sorts of problems :(
                // We need to escape it to %2f, but System.Uri then unescapes it back to '/'
                // The horrible fix is to reset the path field to the original path value, after it's
                // been set.
                var pathField = typeof(Uri).GetField("path", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pathField == null)
                {
                    throw new ApplicationException("Could not resolve path field");
                }
                var alteredPath = (string)pathField.GetValue(uri);
                alteredPath = alteredPath.Replace(@"///", @"/%2f/");
                alteredPath = alteredPath.Replace(@"//", @"/%2f");
                alteredPath = alteredPath.Replace("+", "%2b");
                pathField.SetValue(uri, alteredPath);
            }

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(username, password);
            request.Timeout = request.ReadWriteTimeout = (int)timeout.TotalMilliseconds;
            request.KeepAlive = false; //default WebRequest.KeepAlive to false to resolve spurious 'the request was aborted: the request was canceled' exceptions

            configureRequest(request);

            return request;
        }

        private string BuildEndpointAddress(string path)
        {
            return string.Format("{0}:{1}/api/{2}", hostUrl, portNumber, path);
        }

        private string BuildQueryString(object[] queryObjects)
        {
            if (queryObjects == null || queryObjects.Length == 0)
                return string.Empty;

            StringBuilder queryStringBuilder = new StringBuilder("?");
            var first = true;
            // One or more query objects can be used to build the query
            foreach (var query in queryObjects)
            {
                if (query == null)
                    continue;
                // All public properties are added to the query on the format property_name=value
                var type = query.GetType();
                foreach (var prop in type.GetProperties())
                {
                    var name = Regex.Replace(prop.Name, "([a-z])([A-Z])", "$1_$2").ToLower();
                    var value = prop.GetValue(query, null);
                    if (!first)
                    {
                        queryStringBuilder.Append("&");
                    }
                    queryStringBuilder.AppendFormat("{0}={1}", name, value ?? string.Empty);
                    first = false;
                }
            }
            return queryStringBuilder.ToString();
        }


    }
}

