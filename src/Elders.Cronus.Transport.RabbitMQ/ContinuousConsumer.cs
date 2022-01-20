using System;
using System.Text.Json;
using Elders.Cronus.MessageProcessing;
using Elders.Multithreading.Scheduler;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class ContinuousConsumer<T> : IWork
    {
        protected readonly ILogger logger;

        private readonly ISubscriberCollection<T> subscriberCollection;

        bool stopping;

        public ContinuousConsumer(ISubscriberCollection<T> subscriberCollection, ILogger logger)
        {
            if (subscriberCollection is null) throw new ArgumentNullException(nameof(subscriberCollection));

            this.subscriberCollection = subscriberCollection;
            this.logger = logger;
        }

        public string Name { get; set; }

        public DateTime ScheduledStart { get; set; }

        public void Start()
        {
            try
            {
                if (stopping) return;

                WorkStart();
                while (stopping == false)
                {
                    CronusMessage message = GetMessage();
                    if (ReferenceEquals(null, message)) break;
                    if (message is FailedCronusMessage) continue;
                    try
                    {
                        var subscribers = subscriberCollection.GetInterestedSubscribers(message);
                        foreach (var subscriber in subscribers)
                        {
                            subscriber.Process(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex, () => "Failed to process message." + Environment.NewLine + JsonSerializer.Serialize(message));
                    }
                    finally
                    {
                        MessageConsumed(message);
                    }
                }

                if (stopping)
                    WorkStop();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex, () => "Unexpected Exception.");
                WorkStop();
            }
            finally
            {
                ScheduledStart = DateTime.UtcNow.AddMilliseconds(50);
            }
        }

        public void Stop()
        {
            stopping = true;
        }

        protected abstract void MessageConsumed(CronusMessage message);
        protected abstract void WorkStart();
        protected abstract void WorkStop();
        protected abstract CronusMessage GetMessage();
    }
}
