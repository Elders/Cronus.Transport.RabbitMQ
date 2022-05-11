using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Elders.Cronus.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class RpcHost : IRpcHost
    {
        private readonly ConcurrentBag<AsyncConsumerBase> consumers = new ConcurrentBag<AsyncConsumerBase>();
        private RabbitMqConsumerOptions options;
        private readonly List<object> services;
        private readonly IRequestResponseFactory factory;
        private readonly IServiceProvider provider;
        private readonly ILogger<RpcHost> logger;

        public RpcHost(IServiceProvider provider, IOptionsMonitor<RabbitMqConsumerOptions> options, ILogger<RpcHost> logger, IRequestResponseFactory factory)
        {
            this.options = options.CurrentValue;
            options.OnChange(OptionsChanged);
            services = new List<object>();
            this.factory = factory;
            this.provider = provider;
            this.logger = logger;
        }

        public void Start()
        {
            try
            {
                IDictionary<Type, Type> handlers = factory.GetHandlers();
                foreach (KeyValuePair<Type, Type> handler in handlers)
                {
                    Type requestType = handler.Key;
                    Type responseType = requestType.GetInterfaces().FirstOrDefault().GetGenericArguments().FirstOrDefault();
                    Type endpoint = typeof(IRpc<,>).MakeGenericType(requestType, responseType);

                    services.AddRange(provider.GetServices(endpoint));
                }

                foreach (IRpc service in services)
                {
                    var newcomers = service.StartConsumers();
                    consumers.Add(newcomers.Item1);
                    consumers.Add(newcomers.Item2);
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to start Rpc consumer.")) { }
        }

        private void OptionsChanged(RabbitMqConsumerOptions options)
        {
            if (this.options == options)
                return;

            logger.Info(() => "RabbitMqConsumerOptions changed from {@CurrentOptions} to {@NewOptions}.", this.options, options);

            this.options = options;

            Stop();
            Start();
        }

        public void Stop()
        {
            foreach (var consumer in consumers)
            {
                consumer.StopAsync().GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }


}
