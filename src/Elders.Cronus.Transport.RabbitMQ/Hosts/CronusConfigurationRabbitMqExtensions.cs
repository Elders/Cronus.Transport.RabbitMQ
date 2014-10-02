using System;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.UnitOfWork;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;

namespace Elders.Cronus.Pipeline.Hosts
{
    public static class CronusConfigurationRabbitMqExtensions
    {
        public static T UseDefaultCommandsHostWithRabbitMq<T>(this T self, string boundedContext, Type assemblyContainingMessageHandlers, Func<Type, Context, object> messageHandlerFactory)
            where T : ICronusSettings
        {
            self
                .UseCommandConsumer(boundedContext, consumable => consumable

                    .WithNumberOfConsumersThreads(2)
                    .UseRabbitMqTransport()
                        //.CommandConsumer(consumer => consumer
                        .UseApplicationServices(h => h
                            .UseUnitOfWork(new UnitOfWorkFactory() { CreateBatchUnitOfWork = () => new ApplicationServiceBatchUnitOfWork((self as IHaveEventStores).EventStores[boundedContext].Value.AggregateRepository, (self as IHaveEventStores).EventStores[boundedContext].Value.Persister, self.EventPublisher.Value) })
                            .RegisterAllHandlersInAssembly(assemblyContainingMessageHandlers, messageHandlerFactory)));
            return self;
        }

        public static T WithDefaultPublishersWithRabbitMq<T>(this T self) where T : ICronusSettings
        {
            self
                .UsePipelineEventPublisher(x => x.UseRabbitMqTransport())
                .UsePipelineCommandPublisher(x => x.UseRabbitMqTransport());
            return self;
        }
    }
}