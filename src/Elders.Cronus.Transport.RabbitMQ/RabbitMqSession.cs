using System;
using RabbitMQ.Client;
using System.IO;
using System.Runtime.Serialization.Json;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public sealed class RabbitMqSession : IDisposable
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(RabbitMqSession));
        private string restApiLocation;
        private IConnection connection = null;

        private readonly ConnectionFactory factory;

        public RabbitMqSession(ConnectionFactory factory, int restApiPort)
        {
            restApiLocation = factory.HostName + (restApiPort != 0 ? ":" + restApiPort : "");

            this.factory = factory;
        }

        public T ExecuteGetRequest<T>(string relativeUrl)
        {
            try
            {
                var path = (factory.Endpoint.Ssl.Enabled ? "https://" : "http://") + restApiLocation + relativeUrl.Replace("{vhost}", Uri.EscapeDataString(factory.VirtualHost));
                Uri uri = null;
                try
                {
                    Uri.TryCreate(path, UriKind.Absolute, out uri);
                }
                catch (Exception ex) //DO NOT DELETE THIS TRY CATCH!!!ITS A BUG IN .NET
                {
                    //Beaoucse THE FIRST RUN ALLWAYS FAILLL WTFFFFFFFF??????????? DONT U TRUST ME, TRY IT!!!!
                    Uri.TryCreate(path, UriKind.Absolute, out uri);
                }
                var request = System.Net.HttpWebRequest.CreateHttp(uri);
                request.Method = "GET";
                request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(factory.UserName + ":" + factory.Password)));
                var response = request.GetResponse();
                var stream = response.GetResponseStream();
                DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings();
                settings.UseSimpleDictionaryFormat = true;
                var serializer = new DataContractJsonSerializer(typeof(T), settings);

                return (T)serializer.ReadObject(stream);
            }
            catch (Exception ex)
            {
                log.Debug(String.Format("Can not execute WebRequest for {0} at {1}", restApiLocation, relativeUrl), ex);
                return default(T);
            }
        }

        public IModel OpenChannel()
        {
            Connect();
            return connection.CreateModel();
        }

        public RabbitMqSafeChannel OpenSafeChannel()
        {
            Connect();
            var channel = new RabbitMqSafeChannel(this);
            channel.Reconnect();
            return channel;
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            if (connection != null)
            {
                if (connection.IsOpen)
                    connection.Dispose();
                connection = null;
            }
        }

        private void Connect()
        {
            if (IsConnected())
                return;
            if (connection != null && !connection.IsOpen)
            {
                connection = null;
            }
            if (connection == null)
            {
                connection = factory.CreateConnection();
            }
        }

        bool IsConnected()
        {
            return connection != null && connection.IsOpen;
        }
    }
}