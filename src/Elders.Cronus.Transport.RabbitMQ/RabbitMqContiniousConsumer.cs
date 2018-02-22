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
    public class RabbitMqContinuousConsumer : ContinuousConsumer
    {
        private readonly ISerializer serializer;

        private Dictionary<Guid, ulong> deliveryTags;

        private QueueingBasicConsumerWithManagedConnection consumer;

        public RabbitMqContinuousConsumer(string consumerName, ISerializer serializer, IConnectionFactory connectionFactory, SubscriptionMiddleware middleware) : base(middleware)
        {
            this.deliveryTags = new Dictionary<Guid, ulong>();
            this.serializer = serializer;
            this.consumer = new QueueingBasicConsumerWithManagedConnection(connectionFactory, middleware, consumerName);
        }

        protected override CronusMessage GetMessage()
        {
            if (ReferenceEquals(null, consumer))
                return null;

            return consumer.Do((consumer, subscriber) =>
            {
                BasicDeliverEventArgs dequeuedMessage = null;
                consumer.Queue.Dequeue((int)30, out dequeuedMessage);
                if (ReferenceEquals(null, dequeuedMessage) == false)
                {
                    var cronusMessage = (CronusMessage)serializer.DeserializeFromBytes(dequeuedMessage.Body);
                    deliveryTags[cronusMessage.Id] = dequeuedMessage.DeliveryTag;
                    return cronusMessage;
                }
                return null;
            });
        }

        protected override void MessageConsumed(CronusMessage message)
        {
            if (ReferenceEquals(null, consumer)) return;
            try
            {
                consumer.Do((consumer, subscriber) =>
                {
                    ulong deliveryTag;
                    if (deliveryTags.TryGetValue(message.Id, out deliveryTag))
                        consumer.Model.BasicAck(deliveryTag, false);
                    return true;
                });
            }
            finally
            {
                deliveryTags.Remove(message.Id);
            }

        }

        protected override void WorkStart() { }

        protected override void WorkStop()
        {
            consumer?.Abort();
            consumer = null;
            deliveryTags?.Clear();
            deliveryTags = null;
        }

        class QueueingBasicConsumerWithManagedConnection
        {
            private DateTime timestampSinceConsumerIsNotWorking;
            private IModel model;
            private static IConnection connection;
            private readonly IConnectionFactory connectionFactory;
            private readonly SubscriptionMiddleware middleware;
            private readonly string consumerName;
            private QueueingBasicConsumer consumer;

            public QueueingBasicConsumerWithManagedConnection(IConnectionFactory connectionFactory, SubscriptionMiddleware middleware, string consumerName)
            {
                this.connectionFactory = connectionFactory;
                this.middleware = middleware;
                this.consumerName = consumerName;
            }

            public TResult Do<TResult>(Func<QueueingBasicConsumer, SubscriptionMiddleware, TResult> consumerAction)
            {
                try
                {
                    EnsureHealthyConsumerForSubscriber();
                    TResult result = consumerAction(consumer, middleware);
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
            /// Ensures that we have a running consumer
            /// </summary>
            void EnsureHealthyConsumerForSubscriber()
            {
                RecoverConnection();
                RecoverModel();
                RecoverConsumer();
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
                    var messageTypes = middleware.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Distinct().ToList();

                    foreach (var msgType in messageTypes.Select(x => x.GetContractId()).ToList())
                    {
                        routingHeaders.Add(msgType, null);
                    }

                    model.QueueDeclare(consumerName, true, false, false, routingHeaders);

                    var exchanges = messageTypes.GroupBy(x => RabbitMqNamer.GetExchangeName(x)).Distinct();
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
                        model.QueueBind(consumerName, item.Key, string.Empty, bindHeaders);
                        model.QueueBind(consumerName, item.Key + ".Scheduler", string.Empty, bindHeaders);
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
                        string consumerTag = model.BasicConsume(consumerName, false, consumer);
                    }
                }
            }
        }
    }
}
