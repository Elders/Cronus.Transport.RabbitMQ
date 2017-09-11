using System;
using System.Collections.Generic;
using Elders.Cronus.Serializer;
using Elders.Multithreading.Scheduler;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqEndpoint : IEndpoint, IDisposable
    {
        private readonly ISerializer _serializer;
        private readonly RabbitMqSession _session;
        private readonly int _numberOfWorkers;
        private readonly MessageThreshold _messageThreshold;

        private WorkPool _pool = null;
        private bool _started = false;
        private Action<CronusMessage> _onMessageHandler = null;

        public bool AutoDelete { get; set; }
        public bool Durable { get; private set; }
        public bool Exclusive { get; private set; }
        public string Name { get; private set; }
        public Dictionary<string, object> RoutingHeaders { get; set; }

        public RabbitMqEndpoint(ISerializer serializer, EndpointDefinition endpointDefinition, RabbitMqSession session, Config.IRabbitMqTransportSettings settings)
        {
            this.Name = endpointDefinition.EndpointName;
            this._session = session;
            this._serializer = serializer;
            this.AutoDelete = false;
            this.Exclusive = false;
            this.Durable = true;
            this._numberOfWorkers = Math.Max(1, settings.NumberOfWorkers);
            this._messageThreshold = settings.MessageTreshold ?? new MessageThreshold();

            this.RoutingHeaders = new Dictionary<string, object>();
            foreach (var messageType in endpointDefinition.WatchMessageTypes)
            {
                this.RoutingHeaders.Add(messageType, string.Empty);
            }
        }

        public void OnMessage(Action<CronusMessage> action)
        {
            if (_started)
            {
                throw new Exception("Cannot assign 'onMessageHandler' handler when endpoint was started already");
            }

            _onMessageHandler = action;
        }

        public void Start()
        {
            if (_started)
            {
                throw new Exception("Cannot start endpoint because it's already started");
            }

            if (_onMessageHandler == null)
            {
                throw new Exception("Cannot start endpoint because onMessageHandler handler was not specified yet!");
            }

            _started = true;

            var poolName = String.Format("Workpool {0}", this.Name);
            _pool = new WorkPool(poolName, this._numberOfWorkers);
            for (int i = 0; i < this._numberOfWorkers; i++)
            {
                _pool.AddWork(new RabbitMqEndpointWork(Name, _serializer, _messageThreshold, _onMessageHandler, _session));
            }

            _pool.StartCrawlers();
        }

        public void Stop()
        {
            if (!_started)
            {
                throw new Exception("Cannot stop endpoint because it was not started!");
            }

            _pool.Stop();

            _started = false;
        }

        public void Dispose()
        {
            if (_started)
            {
                this.Stop();
            }
        }

        public void Declare()
        {
            var safeChannel = _session.OpenSafeChannel();
            safeChannel.Channel.QueueDeclare(Name, Durable, Exclusive, AutoDelete, this.RoutingHeaders);
            safeChannel.Close();
        }

        public bool Equals(IEndpoint other)
        {
            throw new NotImplementedException();
        }
    }
}
