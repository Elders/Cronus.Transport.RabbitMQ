using System;
using System.Linq;
using System.Collections.Generic;
using Elders.Cronus.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Serialization;
using System.Threading.Tasks;

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

            hostOptions.OnChange(options =>
            {
                logger.Debug(() => "Cronus host options re-loaded with {@options}", options);

                this.hostOptions = options;
            });
        }

        public async Task StartAsync()
        {
            if (hostOptions.RpcApiEnabled == false)
            {
                logger.LogInformation("Rpc API feature disabled.");
                return;
            }

            try
            {
                var internalWrapperType = typeof(RpcResponse<>);

                ILookup<Type, Type> handlerTypes = factory.GetHandlers();

                foreach (IGrouping<Type, Type> handlers in handlerTypes) // <handler,request>
                {
                    foreach (Type requestType in handlers)
                    {
                        CheckForDataContractNamespace(requestType, () => throw new Exception($"Missing Namespace in DataContract for {requestType.Name}"));

                        Type responseType = requestType.GetInterfaces().FirstOrDefault().GetGenericArguments().FirstOrDefault();
                        if (internalWrapperType.IsAssignableFrom(responseType.GetGenericTypeDefinition()))
                        {
                            var unwrappedResponseType = responseType.GetGenericArguments().FirstOrDefault();
                            CheckForDataContractNamespace(unwrappedResponseType, () => throw new Exception($"Missing Namespace in DataContract for {responseType.Name}"));
                        }
                        else
                        {
                            CheckForDataContractNamespace(responseType, () => throw new Exception($"Missing Namespace in DataContract for {responseType.Name}"));
                        }

                        Type endpoint = typeof(IRpc<,>).MakeGenericType(requestType, responseType);

                        IEnumerable<object> service = provider.GetServices(endpoint);

                        if (service.Contains(null))
                        {
                            logger.LogError($"Unable to resolve endpoint {endpoint.GetInterface("IRpc").Name}<{requestType},{responseType}>.");
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

        private static void CheckForDataContractNamespace(Type type, Action action)
        {
            object[] attributes = type.GetCustomAttributes(typeof(DataContractAttribute), false);
            DataContractAttribute dataContractAttribute = attributes[0] as DataContractAttribute;
            if (dataContractAttribute is not null)
            {
                string @namespace = dataContractAttribute.Namespace;
                if (@namespace is null)
                    action();
            }
        }

        public async Task StopAsync()
        {
            foreach (IRpc service in services)
            {
                await service?.StopConsumersAsync();
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
