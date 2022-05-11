using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    public interface IRpcRequest
    {

    }

    public interface IRpcRequest<TResponse> : IRpcRequest
    {
    }

    public interface IRpcResponse
    {
    }

    public interface IRequestHandler<TRequest, TResponse>
        where TRequest : IRpcRequest<TResponse>
    {
        TResponse HandleAsync(TRequest request);
    }

    public interface IRequestResponseFactory
    {
        void RegisterHandlers(IDictionary<Type, Type> handlers);
        IDictionary<Type, Type> GetHandlers();
        IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>() where TRequest : IRpcRequest<TResponse>;
    }

    public class RequestResponseFactory : IRequestResponseFactory
    {
        private IDictionary<Type, Type> _requestHandlerTypes;
        private readonly IServiceProvider _services;

        public RequestResponseFactory(IServiceProvider services)
        {
            _requestHandlerTypes = new Dictionary<Type, Type>();
            _services = services;
        }

        public void RegisterHandlers(IDictionary<Type, Type> handlers)
        {
            _requestHandlerTypes = handlers;
        }

        public IDictionary<Type, Type> GetHandlers()
        {
            return _requestHandlerTypes;
        }

        public IRequestHandler<TRequest, TResponse> CreateHandler<TRequest, TResponse>() where TRequest : IRpcRequest<TResponse>
        {
            if (!_requestHandlerTypes.ContainsKey(typeof(TRequest)))
                throw new ApplicationException("No handler registered for type: " + typeof(TRequest).FullName);

            Type handlerType = _requestHandlerTypes[typeof(TRequest)];

            IRequestHandler<TRequest, TResponse> handler = (IRequestHandler<TRequest, TResponse>)Activator.CreateInstance(handlerType);

            return handler;
        }
    }
}
