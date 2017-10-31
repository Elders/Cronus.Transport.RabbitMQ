using System;
using System.Collections.Generic;
using System.IO;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqEndpoint : IEndpoint, IDisposable
    {
        private readonly ISerializer _serializer;

        private RabbitMqSafeChannel safeChannel;

        private QueueingBasicConsumer consumer;

        private Dictionary<CronusMessage, BasicDeliverEventArgs> dequeuedMessages;

        private RabbitMqSession session;

        public RabbitMqEndpoint(ISerializer serializer, EndpointDefinition endpointDefinition, RabbitMqSession session)
        {
            this._serializer = serializer;
            AutoDelete = false;
            Exclusive = false;
            Durable = true;
            this.session = session;
            Name = endpointDefinition.EndpointName;
            dequeuedMessages = new Dictionary<CronusMessage, BasicDeliverEventArgs>();

            this.RoutingHeaders = new Dictionary<string, object>();
            foreach (var messageType in endpointDefinition.WatchMessageTypes)
            {
                this.RoutingHeaders.Add(messageType, string.Empty);
            }
        }

        public IDictionary<string, object> RoutingHeaders { get; set; }

        public bool AutoDelete { get; set; }

        public bool Durable { get; private set; }

        public bool Exclusive { get; private set; }

        public string Name { get; private set; }


        public void Acknowledge(CronusMessage message)
        {
            try
            {
                safeChannel.Channel.BasicAck(dequeuedMessages[message].DeliveryTag, false);
                dequeuedMessages.Remove(message);
            }
            catch (EndOfStreamException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception) { Dispose(); throw; }
        }

        public CronusMessage Dequeue(TimeSpan timeout)
        {
            if (!IsInitialized)
            {
                this.Open();
                IsInitialized = true;
            }

            CronusMessage msg = null;
            BasicDeliverEventArgs result;
            if (consumer == null) throw new EndpointClosedException(String.Format("The Endpoint '{0}' is closed", Name));

            try
            {
                if (consumer.Queue.Dequeue((int)timeout.Milliseconds, out result) == false)
                    return null;

                msg = (CronusMessage)_serializer.DeserializeFromBytes(result.Body);
                dequeuedMessages.Add(msg, result);

                return msg;
            }
            catch (EndOfStreamException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Dispose(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception) { Dispose(); throw; }
        }

        public void Dispose()
        {
            safeChannel?.Close();
            safeChannel = null;

            dequeuedMessages.Clear();
        }

        public void Declare()
        {
            if (safeChannel == null)
                safeChannel = session.OpenSafeChannel();

            safeChannel.Channel.QueueDeclare(Name, Durable, Exclusive, AutoDelete, RoutingHeaders);
            safeChannel.Close();
            safeChannel = null;
        }

        private bool IsInitialized = false;
        public void Open()
        {
            if (safeChannel == null)
            {
                safeChannel = session.OpenSafeChannel();
                safeChannel.Channel.BasicQos(0, 1500, false);
                consumer = new QueueingBasicConsumer(safeChannel.Channel);

                safeChannel.Channel.BasicConsume(Name, false, consumer);
                dequeuedMessages.Clear();
            }
            else
            {
                safeChannel.Reconnect();
            }
        }


        public bool Equals(IEndpoint other)
        {
            throw new NotImplementedException();
        }
    }
}
