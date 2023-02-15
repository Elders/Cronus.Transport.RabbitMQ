using System;
using System.Linq;
using System.Collections.Generic;
using Elders.Cronus.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public class RpcHost : IRpcHost
    {
        private CronusHostOptions hostOptions;
        private readonly List<object> services;
        private readonly IRequestResponseFactory factory;
        private readonly IServiceProvider provider;
        private readonly ILogger<RpcHost> logger;

        public RpcHost(IServiceProvider provider, IOptionsMonitor<CronusHostOptions> hostOptions, ILogger<RpcHost> logger, IRequestResponseFactory factory)
        {
            this.hostOptions = hostOptions.CurrentValue;
            this.factory = factory;
            this.provider = provider;
            this.logger = logger;
            services = new List<object>();
        }

        public void Start()
        {
            if (hostOptions.RpcApiEnabled == false)
            {
                logger.LogInformation("Rpc API feature disabled.");
                return;
            }

            try
            {
                ILookup<Type, Type> handlerTypes = factory.GetHandlers();

                foreach (IGrouping<Type, Type> handlers in handlerTypes)
                {
                    foreach (Type handler in handlers)
                    {
                        Type requestType = handler;
                        Type responseType = requestType.GetInterfaces().FirstOrDefault().GetGenericArguments().FirstOrDefault();
                        Type endpoint = typeof(IRpc<,>).MakeGenericType(requestType, responseType);

                        IEnumerable<object> service = provider.GetServices(endpoint);

                        if (service.Contains(null))
                        {
                            logger.LogError($"Unable to resolve endpoind {endpoint.GetInterface("IRpc").Name}<{requestType},{responseType}>.");
                            continue;
                        }

                        services.AddRange(service);
                    }
                }

                // client consumers will be created when we'll try to send a request
                // server consumers will be created for each instance of a handler
                // this separation is necessary in order to avoid unneeded client starts
                foreach (IRpc service in services)
                {
                    service.StartServer();
                }
            }
            catch (Exception ex) when (logger.ErrorException(ex, () => "Failed to start Rpc consumers.")) { }
        }

        public void Stop()
        {
            foreach (IRpc service in services)
            {
                service?.StopConsumersAsync().GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
