using System;
using System.Collections.Generic;
using System.Linq;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class RabbitMqContinuousConsumer<T> : ContinuousConsumer<T>
    {
        private readonly ISerializer serializer;
        private Dictionary<Guid, ulong> deliveryTags;

        private QueueingBasicConsumerWithManagedConnection consumer;

        public RabbitMqContinuousConsumer(BoundedContext boundedContext, ISerializer serializer, IConnectionFactory connectionFactory, ISubscriberCollection<T> subscriberCollection, BoundedContextRabbitMqNamer bcRabbitMqNamer, bool useFanoutMode)
            : base(subscriberCollection)
        {
            this.deliveryTags = new Dictionary<Guid, ulong>();
            this.serializer = serializer;
            this.consumer = new QueueingBasicConsumerWithManagedConnection(connectionFactory, subscriberCollection, boundedContext, bcRabbitMqNamer, useFanoutMode);
        }

        protected override CronusMessage GetMessage()
        {
            if (ReferenceEquals(null, consumer))
                return null;

            return consumer.Do((consumer) =>
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
                consumer.Do((consumer) =>
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
            static readonly ILogger logger = CronusLogger.CreateLogger(typeof(QueueingBasicConsumerWithManagedConnection));

            private DateTime timestampSinceConsumerIsNotWorking;
            private IModel model;
            private static IConnection connection;
            private readonly IConnectionFactory connectionFactory;
            private readonly ISubscriberCollection<T> subscriberCollection;
            private readonly BoundedContext boundedContext;
            private readonly BoundedContextRabbitMqNamer bcRabbitMqNamer;
            private QueueingBasicConsumer consumer;
            private bool aborting;
            private readonly string queueName;
            bool isSystemQueue = false;

            public QueueingBasicConsumerWithManagedConnection(
                IConnectionFactory connectionFactory,
                ISubscriberCollection<T> subscriberCollection,
                BoundedContext boundedContext,
                BoundedContextRabbitMqNamer bcRabbitMqNamer,
                bool useFanoutMode)
            {
                this.connectionFactory = connectionFactory;
                this.subscriberCollection = subscriberCollection;
                this.boundedContext = boundedContext;
                this.bcRabbitMqNamer = bcRabbitMqNamer;
                queueName = GetQueueName(boundedContext.Name, useFanoutMode);
                isSystemQueue = typeof(ISystemHandler).IsAssignableFrom(typeof(T));
            }

            private string GetQueueName(string boundedContext, bool useFanoutMode = false)
            {
                if (useFanoutMode)
                {
                    return $"{boundedContext}.{typeof(T).Name}.{Environment.MachineName}";
                }
                else
                {
                    string systemMarker = typeof(ISystemHandler).IsAssignableFrom(typeof(T)) ? "cronus." : string.Empty;
                    // This is the default
                    return $"{boundedContext}.{systemMarker}{typeof(T).Name}";
                }
            }

            public TResult Do<TResult>(Func<QueueingBasicConsumer, TResult> consumerAction)
            {
                try
                {
                    EnsureHealthyConsumerForSubscriber();
                    TResult result = consumerAction(consumer);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.WarnException(ex, () => ex.Message);
                    return default(TResult);
                }
            }

            public void Abort()
            {
                if (aborting) return;

                lock (connectionFactory)
                {
                    if (aborting) return;

                    aborting = true;
                    consumer = null;

                    model?.Abort();
                    model = null;

                    connection?.Abort(5000);
                    connection = null;
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
                if (aborting)
                    return;

                if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                {
                    lock (connectionFactory)
                    {
                        if (aborting)
                            return;

                        if (ReferenceEquals(null, connection) || connection.IsOpen == false)
                        {
                            connection?.Abort(5000);

                            connection = connectionFactory.CreateConnection();
                        }
                    }
                }
            }

            void RecoverModel()
            {
                if (aborting)
                    return;

                if (model == null || model.IsClosed)
                {
                    model?.Abort();
                    model = connection.CreateModel();
                    model.ConfirmSelect();

                    var routingHeaders = new Dictionary<string, object>();
                    routingHeaders.Add("x-match", "any");
                    var messageTypes = subscriberCollection.Subscribers.SelectMany(x => x.GetInvolvedMessageTypes()).Distinct().ToList();

                    foreach (var msgType in messageTypes.Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                    {
                        routingHeaders.Add(msgType.GetContractId(), msgType.GetBoundedContext(boundedContext.Name));
                    }

                    model.QueueDeclare(queueName, true, false, false, routingHeaders);

                    var exchangeGroups = messageTypes
                        .SelectMany(mt => bcRabbitMqNamer.GetExchangeNames(mt).Select(x => new { Exchange = x, MessageType = mt }))
                        .GroupBy(x => x.Exchange)
                        .Distinct();
                    foreach (var exchangeGroup in exchangeGroups)
                    {
                        model.ExchangeDeclare(exchangeGroup.Key, PipelineType.Headers.ToString(), true);
                        var args = new Dictionary<string, object>();
                        args.Add("x-delayed-type", PipelineType.Headers.ToString());
                        model.ExchangeDeclare(exchangeGroup.Key + ".Scheduler", "x-delayed-message", true, false, args);

                        var bindHeaders = new Dictionary<string, object>();
                        bindHeaders.Add("x-match", "any");

                        foreach (Type msgType in exchangeGroup.Select(x => x.MessageType).Where(mt => typeof(ISystemMessage).IsAssignableFrom(mt) == isSystemQueue))
                        {
                            bindHeaders.Add(msgType.GetContractId(), msgType.GetBoundedContext(boundedContext.Name));
                        }
                        model.QueueBind(queueName, exchangeGroup.Key, string.Empty, bindHeaders);
                        model.QueueBind(queueName, exchangeGroup.Key + ".Scheduler", string.Empty, bindHeaders);
                        model.BasicQos(0, 1, false);
                    }
                }
            }

            void RecoverConsumer()
            {
                if (aborting)
                    return;

                if (consumer == null || consumer.IsRunning == false)
                {
                    if ((DateTime.UtcNow - timestampSinceConsumerIsNotWorking).TotalSeconds > 50)
                    {
                        timestampSinceConsumerIsNotWorking = DateTime.UtcNow;

                        consumer = new QueueingBasicConsumer(model);
                        string consumerTag = model.BasicConsume(queueName, false, consumer);

                        if (consumer.IsRunning == false)
                            throw new Exception("Unable to start QueueingBasicConsumerWithManagedConnection. Terminating the connection.");
                    }
                }
            }
        }
    }
}
