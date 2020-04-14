using System;
using Elders.Cronus.MessageProcessing;
using Elders.Multithreading.Scheduler;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class ContinuousConsumer<T> : IWork
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(ContinuousConsumer<>));

        ISubscriberCollection<T> subscriberCollection;

        bool stopping;

        public ContinuousConsumer(ISubscriberCollection<T> subscriberCollection)
        {
            if (subscriberCollection is null) throw new ArgumentNullException(nameof(subscriberCollection));

            this.subscriberCollection = subscriberCollection;
        }

        public string Name { get; set; }

        public DateTime ScheduledStart { get; set; }

        protected abstract void MessageConsumed(CronusMessage message);
        protected abstract void WorkStart();
        protected abstract void WorkStop();
        protected abstract CronusMessage GetMessage();

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
                        logger.ErrorException("Failed to process message.", ex);
                    }
                    finally
                    {
                        MessageConsumed(message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Unexpected Exception.", ex);
            }
            finally
            {
                ScheduledStart = DateTime.UtcNow.AddMilliseconds(50);
            }
        }

        public void Stop()
        {
            stopping = true;
            WorkStop();
        }
    }
}
