using System;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.UnitOfWork;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;

namespace Elders.Cronus.Pipeline.Hosts
{
    public static class CronusConfigurationRabbitMqExtensions
    {
        //public static T UseDefaultCommandsHostWithRabbitMq<T>(this T self, Type assemblyContainingMessageHandlers, Func<Type, Context, object> messageHandlerFactory)
        //    where T : ICronusSettings
        //{
        //    self
        //        .UseCommandConsumer(consumable => consumable
        //            .SetNumberOfConsumerThreads(2)
        //            .UseRabbitMqTransport()
        //                .UseApplicationServices(h => h
        //                    //.UseUnitOfWork(new UnitOfWorkFactory() { CreateBatchUnitOfWork = () => new ApplicationServiceBatchUnitOfWork((self as IHaveEventStores).EventStores[boundedContext].Value.AggregateRepository, (self as IHaveEventStores).EventStores[boundedContext].Value.Persister, self.EventPublisher.Value) })
        //                    .RegisterAllHandlersInAssembly(assemblyContainingMessageHandlers, messageHandlerFactory)));
        //    return self;
        //}

        //public static T UseDefaultCommandsHostWithRabbitMq<T>(this T self, Action<ICronusSettings> configure = null)
        //    where T : ICronusSettings
        //{
        //    self
        //        .UseCommandConsumer(consumable => consumable
        //            .SetNumberOfConsumerThreads(2)
        //            .UseRabbitMqTransport()
        //                .UseApplicationServices(h => h
        //                    //.UseUnitOfWork(new UnitOfWorkFactory() { CreateBatchUnitOfWork = () => new ApplicationServiceBatchUnitOfWork((self as IHaveEventStores).EventStores[boundedContext].Value.AggregateRepository, (self as IHaveEventStores).EventStores[boundedContext].Value.Persister, self.EventPublisher.Value) })
        //                    .RegisterAllHandlersInAssembly(assemblyContainingMessageHandlers, messageHandlerFactory)));

        //    if (configure != null)
        //        configure(self);
        //    return self;
        //}

        public static T WithDefaultPublishersWithRabbitMq<T>(this T self) where T : IConsumerSettings
        {
            self
                .UsePipelineEventPublisher()
                .UsePipelineCommandPublisher();
            return self;
        }



    }
}