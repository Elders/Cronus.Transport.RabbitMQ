using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Elders.Cronus.Transport.RabbitMQ.RpcAPI;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    [CronusStartup(Bootstraps.Environment)]
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
                IDictionary<Type, Type> handlers = GetAllHandlers();
                requestFactory.RegisterHandlers(handlers);
            }
        }

        public IDictionary<Type, Type> GetAllHandlers()
        {
            Dictionary<Type, Type> handlers = new DefaulAssemblyScanner()
                .Scan()
                 .Where(t => !t.IsAbstract)
                   .Select(t => new
                   {
                       HandlerType = t,
                       RequestType = GetHandledRequestType(t)
                   })
                   .Where(x => x.RequestType != null)
                   .ToDictionary(
                       x => x.RequestType,
                       x => x.HandlerType
                   );
            return handlers;
        }

        private Type GetHandledRequestType(Type type)
        {
            Type handlerInterface = type.GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            return handlerInterface == null ? null : handlerInterface.GetGenericArguments()[0];
        }
    }
}
