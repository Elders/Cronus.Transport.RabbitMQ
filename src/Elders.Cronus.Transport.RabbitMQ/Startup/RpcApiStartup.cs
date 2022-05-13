using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Elders.Cronus.Transport.RabbitMQ.RpcAPI;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    [CronusStartup(Bootstraps.Runtime)]
    public class RpcApiStartup : ICronusStartup
    {
        private readonly CronusHostOptions hostOptions;
        private readonly IRequestResponseFactory requestFactory;

        public RpcApiStartup(IOptionsMonitor<CronusHostOptions> cronusHostOptions, IRequestResponseFactory requestFactory)
        {
            this.hostOptions = cronusHostOptions.CurrentValue;
            this.requestFactory = requestFactory;
        }

        public void Bootstrap()
        {
            if (hostOptions.RpcApiEnabled)
            {
                ILookup<Type, Type> handlers = GetHandlers();
                requestFactory.RegisterHandlers(handlers);
            }
        }

        public ILookup<Type, Type> GetHandlers()
        {
            ILookup<Type, Type> handlers = new DefaulAssemblyScanner()
                 .Scan()
                  .Where(t => t.IsAbstract == false)
                    .Select(t => new
                    {
                        HandlerType = t,
                        RequestTypes = GetHandledRequestTypes(t)
                    })
                    .Where(x => x.RequestTypes.Any())
                    .SelectMany(p => p.RequestTypes.Select(r => new { p.HandlerType, RequestType = r }))
                  .ToLookup(pair => pair.HandlerType, pair => pair.RequestType);

            return handlers;
        }

        private IEnumerable<Type> GetHandledRequestTypes(Type type)
        {
            IEnumerable<Type> handlerInterfaces = type.GetInterfaces()
                 .Where(i =>
                     i.IsGenericType &&
                     i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            return handlerInterfaces.Select(handlerInterface => handlerInterface is null ? null : handlerInterface.GetGenericArguments()[0]);
        }
    }
}
