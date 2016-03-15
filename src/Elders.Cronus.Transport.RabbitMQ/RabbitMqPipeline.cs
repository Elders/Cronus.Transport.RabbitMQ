using System;
using System.IO;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public sealed class RabbitMqPipeline : IRabbitMqPipeline, IDisposable
    {
        private RabbitMqSafeChannel safeChannel;
        private readonly PipelineType pipelineType;
        private RabbitMqSession session;
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(RabbitMqPipeline));
        private string name;

        public RabbitMqPipeline(string pipelineName, RabbitMqSession rabbitMqSession, PipelineType pipelineType)
        {
            this.pipelineType = pipelineType;
            this.name = pipelineName;
            this.session = rabbitMqSession;
        }

        public void Dispose()
        {
            Close();
        }

        public void Open()
        {
            safeChannel = session.OpenSafeChannel();
        }

        public void Close()
        {
            if (safeChannel != null)
            {
                lock (safeChannel)
                {
                    if (safeChannel != null)
                    {
                        safeChannel.Close();
                        safeChannel = null;
                    }
                }
            }
        }

        public void Push(EndpointMessage message)
        {
            if (safeChannel == null)
            {
                throw new PipelineClosedException(String.Format("The Pipeline '{0}' is closed", name));
            }
            try
            {
                IBasicProperties properties = new BasicProperties();
                properties.Headers = message.RoutingHeaders;
                properties.Persistent = true;
                properties.Priority = 9;
                safeChannel.Channel.BasicPublish(name, message.RoutingKey, false, properties, message.Body);
            }
            catch (EndOfStreamException ex)
            { throw new PipelineClosedException(String.Format("The Pipeline '{0}' was closed", name), ex); }
            catch (AlreadyClosedException ex)
            { throw new PipelineClosedException(String.Format("The Pipeline '{0}' was closed", name), ex); }
            catch (OperationInterruptedException ex)
            { throw new PipelineClosedException(String.Format("The Pipeline '{0}' was closed", name), ex); }

        }

        [DataContract]
        internal class Binding
        {
            [DataMember(Name = "routing_key")]
            public string Routing_Key { get; set; }

            [DataMember(Name = "destination")]
            public string Destination { get; set; }

            [DataMember(Name = "source")]
            public string Source { get; set; }

            [DataMember(Name = "arguments")]
            public Dictionary<string, string> Arguments { get; set; }

            public Dictionary<string, object> Headers
            {
                get
                {
                    var dc = new Dictionary<string, object>();
                    foreach (var item in Arguments)
                    {
                        dc.Add(item.Key, item.Value);
                    }
                    return dc;
                }
            }
        }

        public void Bind(IEndpoint endpoint)
        {
            if (safeChannel == null)
            {
                safeChannel = session.OpenSafeChannel();
            }

            var bindings = session.ExecuteGetRequest<List<Binding>>("/api/queues/{vhost}/" + endpoint.Name + "/bindings");
            if (bindings == null)
                log.Warn("Rest api did not return any bindings. We could not remove anybindings.");
            else
            {
                var nonDefaultBindings = bindings.Where(x => x.Source == name);
                foreach (var item in nonDefaultBindings)
                {
                    safeChannel.Channel.QueueUnbind(endpoint.Name, item.Source, item.Routing_Key, item.Headers);
                }
            }

            safeChannel.Channel.QueueBind(endpoint.Name, name, endpoint.RoutingKey, endpoint.RoutingHeaders);
            safeChannel.Close();
            safeChannel = null;
        }

        public void Declare()
        {
            if (safeChannel == null)
            {
                safeChannel = session.OpenSafeChannel();
            }

            safeChannel.Channel.ExchangeDeclare(name, pipelineType.ToString());
            safeChannel.Close();
            safeChannel = null;
        }

        public sealed class PipelineType
        {
            private readonly string name;
            private readonly int value;

            public static readonly PipelineType Direct = new PipelineType(1, "direct");
            public static readonly PipelineType Fanout = new PipelineType(2, "fanout");
            public static readonly PipelineType Headers = new PipelineType(3, "headers");
            public static readonly PipelineType Topics = new PipelineType(4, "topic");

            private PipelineType(int value, string name)
            {
                this.name = name;
                this.value = value;
            }

            public override String ToString()
            {
                return name;
            }

        }
        public string Name
        {
            get { return name; }
        }

        public bool Equals(IPipeline other)
        {
            throw new NotImplementedException();
        }
    }
}