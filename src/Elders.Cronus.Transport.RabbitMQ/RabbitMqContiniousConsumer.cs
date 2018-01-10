using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Pipeline;
using Elders.Cronus.Serializer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqContiniousConsumer : ContinuousConsumer
    {
        private readonly ISerializer serializer;

        private readonly QueueingBasicConsumerWithManagedConnection consumer;

        public RabbitMqContiniousConsumer(ISubscriber subscriber, ISerializer serializer, IConnectionFactory connectionFactory, SubscriptionMiddleware middleware) : base(middleware)
        {
            this.serializer = serializer;
            this.consumer = new QueueingBasicConsumerWithManagedConnection(connectionFactory, subscriber);
        }

        protected override CronusMessage GetMessage()
        {
            return consumer.Do((consumer, subscriber) =>
            {
                BasicDeliverEventArgs dequeuedMessage = null;
                consumer.Queue.Dequeue((int)30, out dequeuedMessage);

                var cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(dequeuedMessage.Body);
                cronusMessage.Headers.Add("rmq-deliverytag", dequeuedMessage.DeliveryTag.ToString());
                return cronusMessage;
            });
        }

        protected override void MessageConsumed(CronusMessage message)
        {
            consumer.Do((consumer, subscriber) =>
            {
                ulong deliveryTag = ulong.Parse(message.Headers["rmq-deliverytag"]);
                consumer.Model.BasicAck(deliveryTag, false);
                return true;
            });
        }

        protected override void WorkStart() { }

        protected override void WorkStop()
        {
            consumer.Abort();
        }

        class QueueingBasicConsumerWithManagedConnection
        {
            private DateTime timestampSinceConsumerIsNotWorking;
            private IModel model;
            private static IConnection connection;
            private readonly IConnectionFactory connectionFactory;
            private readonly ISubscriber subscriber;
            private QueueingBasicConsumer consumer;

            public QueueingBasicConsumerWithManagedConnection(IConnectionFactory connectionFactory, ISubscriber subscriber)
            {
                this.connectionFactory = connectionFactory;
                this.subscriber = subscriber;
            }

            public TResult Do<TResult>(Func<QueueingBasicConsumer, ISubscriber, TResult> consumerAction)
            {
                try
                {
                    EnsureHealthyConsumerForSubscriber();
                    TResult result = consumerAction(consumer, subscriber);
                    return result;
                }
                catch (Exception ex)
                {
                    return default(TResult);
                }
            }

            public void Abort()
            {
                lock (connectionFactory)
                {
                    model?.Abort();
                    connection?.Abort();

                    connection = null;
                    model = null;
                    consumer = null;
                }
            }

            /// <summary>
            /// Reinitializes the consumer if it is not running or forced.
            /// </summary>
            /// <param name="force"></param>
            /// <returns>Returns TRUE only if the consumer was reinitialized./></returns>
            bool EnsureHealthyConsumerForSubscriber(bool force = false)
            {
                RecoverConnection();
                RecoverModel();
                RecoverConsumer();

                return false;
            }

            /// <summary>
            /// By rabbitmq design, Each IConnection instance is, in the current implementation, backed by a single background thread that reads from the socket and dispatches the resulting events to the application.
            /// So the connection MUST be one per process so we need to ensure that we have only one. This is the reason for the lock
            /// </summary>
            void RecoverConnection()
            {
                if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                {
                    lock (connectionFactory)
                    {
                        if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                        {
                            connection?.Abort();
                            connection = connectionFactory.CreateConnection();
                            connection.AutoClose = false;
                        }
                    }
                }
            }

            void RecoverModel()
            {
                if (model == null || model.IsClosed)
                {
                    model = connection.CreateModel();

                    var routingHeaders = new Dictionary<string, object>();
                    routingHeaders.Add("x-match", "any");
                    foreach (var msgType in subscriber.MessageTypes.Distinct().Select(x => x.GetContractId()).ToList())
                    {
                        routingHeaders.Add(msgType, null);
                    }
                    model.QueueDeclare(subscriber.Id, true, false, false, routingHeaders);

                    var exchanges = subscriber.MessageTypes.GroupBy(x => RabbitMqNamer.GetExchangeName(x)).Distinct();
                    foreach (var item in exchanges)
                    {
                        model.ExchangeDeclare(item.Key, PipelineType.Headers.ToString(), true);
                        var args = new Dictionary<string, object>();
                        args.Add("x-delayed-type", PipelineType.Headers.ToString());
                        model.ExchangeDeclare(item.Key + ".Scheduler", "x-delayed-message", true, false, args);

                        var bindHeaders = new Dictionary<string, object>();
                        bindHeaders.Add("x-match", "any");

                        foreach (var msgType in item.Distinct().Select(x => x.GetContractId()).ToList())
                        {
                            bindHeaders.Add(msgType, null);
                        }
                        model.QueueBind(subscriber.Id, item.Key, string.Empty, bindHeaders);
                        model.QueueBind(subscriber.Id, item.Key + ".Scheduler", string.Empty, bindHeaders);
                        model.BasicQos(0, 1500, false);
                    }
                }
            }

            void RecoverConsumer()
            {
                if (consumer == null || consumer.IsRunning == false)
                {
                    if ((DateTime.UtcNow - timestampSinceConsumerIsNotWorking).TotalSeconds > 5)
                    {
                        timestampSinceConsumerIsNotWorking = DateTime.UtcNow;
                        consumer = new QueueingBasicConsumer(model);
                        string consumerTag = model.BasicConsume(subscriber.Id, false, consumer);
                    }
                }
            }
        }
    }
}
