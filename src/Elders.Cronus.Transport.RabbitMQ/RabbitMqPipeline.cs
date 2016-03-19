using System;
using System.Collections.Generic;
using System.IO;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class UberPipeline : IRabbitMqPipeline, IDisposable
    {
        protected RabbitMqSafeChannel safeChannel;

        readonly protected string name;
        readonly protected PipelineType pipelineType;

        readonly private IRabbitMqPipeline pipeline;
        readonly private IRabbitMqPipeline scheduler;

        public UberPipeline(string pipelineName, RabbitMqSession session, PipelineType pipelineType)
        {
            this.pipelineType = pipelineType;
            this.name = pipelineName;

            this.pipeline = new RabbitMqPipeline(pipelineName, session, pipelineType);
            this.scheduler = new SchedulerPipeline(pipelineName + ".Scheduler", session, pipelineType);
        }

        public string Name { get { return name; } }

        public void Open()
        {
            pipeline.Open();
            scheduler.Open();
        }

        public void Close()
        {
            pipeline.Close();
            scheduler.Close();
        }

        public void Declare()
        {
            pipeline.Declare();
            scheduler.Declare();
        }

        public void Push(EndpointMessage message)
        {
            if (message.PublishDelayInMiliseconds == long.MinValue)
                pipeline.Push(message);
            else
                scheduler.Push(message);
        }

        public void Bind(IEndpoint endpoint)
        {
            pipeline.Bind(endpoint);
            scheduler.Bind(endpoint);
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
        public SchedulerPipeline(string pipelineName, RabbitMqSession session, PipelineType pipelineType)
            : base(pipelineName, session, pipelineType)
        { }

        public override void Bind(IEndpoint endpoint)
        {
            // TODO: ClearOldHeaders
            Open();
            safeChannel.Channel.QueueBind(endpoint.Name, name, endpoint.RoutingKey, endpoint.RoutingHeaders);
            Close();
        }

        public override void Declare()
        {
            Open();
            var args = new Dictionary<string, object>();
            args.Add("x-delayed-type", pipelineType.ToString());
            safeChannel.Channel.ExchangeDeclare(name, "x-delayed-message", true, false, args);
            Close();
        }

        public override void Push(EndpointMessage message)
        {
            if (ReferenceEquals(null, safeChannel)) throw new PipelineClosedException($"The Pipeline '{name}' was closed.");

            try
            {
                message.RoutingHeaders.Add("x-delay", message.PublishDelayInMiliseconds);
                IBasicProperties properties = new BasicProperties();
                properties.Headers = message.RoutingHeaders;
                properties.Persistent = true;
                properties.Priority = 9;
                safeChannel.Channel.BasicPublish(name, message.RoutingKey, false, properties, message.Body);
            }
            catch (EndOfStreamException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (AlreadyClosedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
            catch (OperationInterruptedException ex) { throw new PipelineClosedException($"The Pipeline '{name}' was closed.", ex); }
        }
    }

    public class RabbitMqPipeline : IRabbitMqPipeline, IDisposable
    {
        protected RabbitMqSafeChannel safeChannel;

        readonly protected string name;
        readonly protected RabbitMqSession session;
        readonly protected PipelineType pipelineType;

        public RabbitMqPipeline(string pipelineName, RabbitMqSession session, PipelineType pipelineType)
        {
            this.pipelineType = pipelineType;
            this.name = pipelineName;
            this.session = session;
        }

        public string Name { get { return name; } }

        public void Open()
        {
            if (ReferenceEquals(null, safeChannel))
                safeChannel = session.OpenSafeChannel();
        }

        public virtual void Push(EndpointMessage message)
        {
            if (ReferenceEquals(null, safeChannel)) throw new PipelineClosedException($"The Pipeline '{name}' was closed.");

            try
            {
                Open();
                IBasicProperties properties = new BasicProperties();
                properties.Headers = message.RoutingHeaders;
                properties.Persistent = true;
                properties.Priority = 9;
                safeChannel.Channel.BasicPublish(name, message.RoutingKey, false, properties, message.Body);
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
            // TODO: ClearOldHeaders
            Open();
            safeChannel.Channel.QueueBind(endpoint.Name, name, endpoint.RoutingKey, endpoint.RoutingHeaders);
            Close();
        }

        public virtual void Declare()
        {
            Open();
            safeChannel.Channel.ExchangeDeclare(name, pipelineType.ToString(), true);
            Close();
        }

        public void Close()
        {
            if (ReferenceEquals(null, safeChannel) == false)
            {
                lock (safeChannel)
                {
                    if (ReferenceEquals(null, safeChannel) == false)
                    {
                        safeChannel.Close();
                        safeChannel = null;
                    }
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