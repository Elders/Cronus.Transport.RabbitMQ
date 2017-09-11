using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Logging;
using Elders.Cronus.Serializer;
using Elders.Multithreading.Scheduler;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Pipeline.Transport.RabbitMQ
{
    public class RabbitMqEndpointWork : IWork
    {
        private readonly string _name;
        private readonly RabbitMqSession _session;
        private readonly ISerializer _serializer;
        private readonly MessageThreshold _messageThreshold;
        private readonly Action<CronusMessage> _onMessageHandler;

        private static readonly ILog log = LogProvider.GetLogger(typeof(RabbitMqEndpointWork));

        private volatile bool _isWorking;
        private QueueingBasicConsumer _consumer;
        private RabbitMqSafeChannel _safeChannel;

        public RabbitMqEndpointWork(
            string name,
            ISerializer serializer,
            MessageThreshold messageThreshold,
            Action<CronusMessage> onMessageHandler,
            RabbitMqSession session)
        {
            this._name = name;
            this._serializer = serializer;
            this._messageThreshold = messageThreshold;
            this._onMessageHandler = onMessageHandler;
            this._session = session;
        }

        public DateTime ScheduledStart { get; set; }

        public void Open()
        {
            if (_safeChannel == null)
            {
                _safeChannel = _session.OpenSafeChannel();
                _safeChannel.Channel.BasicQos(0, 1500, false);
                _consumer = new QueueingBasicConsumer(_safeChannel.Channel);

                _safeChannel.Channel.BasicConsume(_name, false, _consumer);
                //dequeuedMessages.Clear();
            }
            else
            {
                _safeChannel.Reconnect();
            }
        }

        public bool BlockDequeue(uint timeoutInMiliseconds, out CronusMessage msg, out BasicDeliverEventArgs result)
        {
            msg = null;
            result = null;
            if (_consumer == null) throw new EndpointClosedException(String.Format("The Endpoint '{0}' is closed", _name));
        
            try
            {
                BasicDeliverEventArgs res;
                if (_consumer.Queue.Dequeue((int)timeoutInMiliseconds, out res) == false)
                    return false;

                result = res;
                msg = (CronusMessage)_serializer.DeserializeFromBytes(result.Body);

                return true;
            }
            catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
            catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
            catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
            catch (Exception) { Close(); throw; }
        }

        public void Start()
        {
            var dequeuedMessaged = new List<BasicDeliverEventArgs>();

            try
            {
                _isWorking = true;

                this.Open();

                while (_isWorking)
                {
                    for (int i = 0; i < _messageThreshold.Size; i++)
                    {
                        CronusMessage msg = null;
                        BasicDeliverEventArgs rawMsg = null;
                        if (!BlockDequeue(_messageThreshold.Delay, out msg, out rawMsg))
                            break;

                        dequeuedMessaged.Add(rawMsg);

                        _onMessageHandler(msg);
                    }

                    AcknowledgeAll(dequeuedMessaged);
                }
            }
            catch (EndpointClosedException ex)
            {
                log.DebugException("Endpoint Closed", ex);
            }
            catch (Exception ex)
            {
                log.ErrorException("Unexpected Exception.", ex);
            }
            finally
            {
                try
                {
                    AcknowledgeAll(dequeuedMessaged);
                }
                catch (EndpointClosedException ex)
                {
                    log.DebugException("Endpoint Closed", ex);
                }
                ScheduledStart = DateTime.UtcNow.AddMilliseconds(30);
            }
        }

        private void AcknowledgeAll(List<BasicDeliverEventArgs> list)
        {
            foreach (var msg in list.ToList())
            {
                try
                {
                    _safeChannel.Channel.BasicAck(msg.DeliveryTag, false);
                    list.Remove(msg);
                }
                catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
                catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
                catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", _name), ex); }
                catch (Exception) { Close(); throw; }
            }
        }

        public void Close()
        {
            _safeChannel?.Close();
            _safeChannel = null;
        }

        public void Stop()
        {
            _isWorking = false;
            this.Close();
        }
    }
}