using System;
using System.Collections.Generic;
using System.IO;
using Elders.Cronus;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class UberPipeline : IRabbitMqPipeline, IDisposable
    {
        private readonly string _name;
        private readonly PipelineType _pipelineType;

        private readonly IRabbitMqPipeline _pipeline;
        private readonly IRabbitMqPipeline _scheduler;

        public string Name { get { return _name; } }

        public UberPipeline(ISerializer serializer, string pipelineName, RabbitMqSession session, PipelineType pipelineType)
        {
            this._pipelineType = pipelineType;
            this._name = pipelineName;
            this._pipeline = new RabbitMqPipeline(serializer, pipelineName, session, pipelineType);
            this._scheduler = new SchedulerPipeline(serializer, pipelineName + ".Scheduler", session, pipelineType);
        }

        public void Open()
        {
            _pipeline.Open();
            _scheduler.Open();
        }

        public void Close()
        {
            _pipeline.Close();
            _scheduler.Close();
        }

        public void Declare()
        {
            _pipeline.Declare();
            _scheduler.Declare();
        }

        public void Push(CronusMessage message)
        {
            var publishDelayInMiliseconds = message.GetPublishDelay();
            if (publishDelayInMiliseconds == long.MinValue)
                _pipeline.Push(message);
            else
                _scheduler.Push(message);
        }

        public void Bind(IEndpoint endpoint)
        {
            _pipeline.Bind(endpoint);
            _scheduler.Bind(endpoint);
        }

        public bool Equals(IPipeline other)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Close();
        }
    }

    public class SchedulerPipeline : RabbitMqPipeline
    {
        public SchedulerPipeline(ISerializer serializer, string pipelineName, RabbitMqSession session, PipelineType pipelineType)
            : base(serializer, pipelineName, session, pipelineType)
        { }

        public override void Declare()
        {
            Open();
            var args = new Dictionary<string, object>();
            args.Add("x-delayed-type", _pipelineType.ToString());
            safeChannel.Channel.ExchangeDeclare(name, "x-delayed-message", true, false, args);
            Close();
        }

        public override void Push(CronusMessage message)
        {
            if (ReferenceEquals(null, safeChannel)) throw new PipelineClosedException($"The Pipeline '{name}' was closed.");

            try
            {
                var routingHeaders = new Dictionary<string, object>();
                routingHeaders.Add(message.Payload.GetType().GetContractId(), String.Empty);
                routingHeaders.Add("x-delay", message.GetPublishDelay());

                IBasicProperties properties = new BasicProperties();
                properties.Headers = routingHeaders;
                properties.Persistent = true;
                properties.Priority = 9;

                byte[] body = this.serializer.SerializeToBytes(message);

                safeChannel.Channel.BasicPublish(name, string.Empty, false, properties, body);
            }
            catch (EndOfStreamException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (AlreadyClosedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (OperationInterruptedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
        }
    }

    public class RabbitMqPipeline : IRabbitMqPipeline, IDisposable
    {
        protected readonly ISerializer serializer;
        protected readonly string name;
        protected readonly RabbitMqSession session;
        protected readonly PipelineType _pipelineType;

        protected RabbitMqSafeChannel safeChannel;

        public RabbitMqPipeline(ISerializer serializer, string pipelineName, RabbitMqSession session, PipelineType pipelineType)
        {
            this.serializer = serializer;
            this._pipelineType = pipelineType;
            this.name = pipelineName;
            this.session = session;
        }

        public string Name { get { return name; } }

        public void Open()
        {
            if (ReferenceEquals(null, safeChannel))
                safeChannel = session.OpenSafeChannel();
        }

        public virtual void Push(CronusMessage message)
        {
            if (ReferenceEquals(null, safeChannel)) throw new PipelineClosedException($"The Pipeline '{name}' was closed.");

            try
            {
                Open();
                IBasicProperties properties = new BasicProperties();

                var routingHeaders = new Dictionary<string, object>();
                routingHeaders.Add(message.Payload.GetType().GetContractId(), String.Empty);

                properties.Headers = routingHeaders;
                properties.Persistent = true;
                properties.Priority = 9;

                byte[] body = this.serializer.SerializeToBytes(message);

                safeChannel.Channel.BasicPublish(name, string.Empty, false, properties, body);
            }
            catch (EndOfStreamException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (AlreadyClosedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (OperationInterruptedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            finally
            {
                Close();
            }
        }

        public virtual void Bind(IEndpoint endpoint)
        {
            var rabbitMq = endpoint as RabbitMqEndpoint;

            if (rabbitMq == null)
            {
                throw new Exception("Cannot mix RabbitMq pipeline & non-RabbitMq endpoint!");
            }

            // TODO: ClearOldHeaders
            Open();
            safeChannel.Channel.QueueBind(endpoint.Name, name, string.Empty, rabbitMq.RoutingHeaders);
            Close();
        }

        public virtual void Declare()
        {
            Open();
            safeChannel.Channel.ExchangeDeclare(name, _pipelineType.ToString(), true);
            Close();
        }

        public void Close()
        {
            if (ReferenceEquals(null, safeChannel) == false)
            {
                lock (safeChannel)
                {
                    safeChannel?.Close();
                    safeChannel = null;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }

        public bool Equals(IPipeline other)
        {
            throw new NotImplementedException();
        }
    }
}
