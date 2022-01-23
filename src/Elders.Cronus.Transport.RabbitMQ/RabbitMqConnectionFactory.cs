﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public interface IRabbitMqConnectionFactory
    {
        public IConnection CreateConnection();

        public IConnection CreateNewConnection(IRabbitMqOptions options);
        //Task CloseConnectionAsync<T>(IConnection connection, AsyncConsumerFactory<T> asyncConsumerFactory);
    }

    public class RabbitMqConnectionFactory<TOptions> : IRabbitMqConnectionFactory where TOptions : IRabbitMqOptions
    {
        static readonly ILogger logger = CronusLogger.CreateLogger(typeof(RabbitMqConnectionFactory<TOptions>));
        private readonly TOptions options;
        private readonly RabbitMqInfrastructure rabbitMqInfrastructure;
        private readonly ConnectionFactory connectionFactory;
        private IConnection connection;

        public RabbitMqConnectionFactory()
        {
        }

        public RabbitMqConnectionFactory(RabbitMqInfrastructure rabbitMqInfrastructure, IOptionsMonitor<TOptions> settings, ConnectionFactory connectionFactory) : this()
        {
            options = settings.CurrentValue;
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            this.rabbitMqInfrastructure = rabbitMqInfrastructure;
            this.connectionFactory = connectionFactory;
            this.connectionFactory.HostName = options.Server;
            this.connectionFactory.Port = options.Port;
            this.connectionFactory.UserName = options.Username;
            this.connectionFactory.Password = options.Password;
            this.connectionFactory.VirtualHost = options.VHost;
            this.connectionFactory.DispatchConsumersAsync = true;
            this.connectionFactory.AutomaticRecoveryEnabled = true;
            this.connectionFactory.EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
        }

        public IConnection CreateConnection()
        {
            try
            {
                connection = this.connectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
                return connection;
            }
            catch (BrokerUnreachableException)
            {
                Thread.Sleep(1000);
                rabbitMqInfrastructure.Initialize();

                connection = this.connectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
                return connection;
            }
        }

        public IConnection CreateNewConnection(IRabbitMqOptions options)
        {
            logger.Debug(() => "Loaded RabbitMQ options are {@Options}", options);
            var newConnectionFactory = new ConnectionFactory();
            newConnectionFactory.HostName = options.Server;
            newConnectionFactory.Port = options.Port;
            newConnectionFactory.UserName = options.Username;
            newConnectionFactory.Password = options.Password;
            newConnectionFactory.VirtualHost = options.VHost;
            newConnectionFactory.DispatchConsumersAsync = true;
            newConnectionFactory.AutomaticRecoveryEnabled = true;
            newConnectionFactory.EndpointResolverFactory = (x) => { return new MultipleEndpointResolver(options); };
            return newConnectionFactory.CreateConnection(new MultipleEndpointResolver(options).All().ToList());
        }

        //public Task CloseConnectionAsync<T>(IConnection connection, AsyncConsumerFactory<T> factory)
        //{
        //    foreach (var subscriber in factory.channels)
        //    {
        //        while (factory.isAcked == false)
        //        {
        //            continue;
        //        }

        //        if (factory.isAcked == true)
        //        {
        //            subscriber.Close();
        //        }
        //    }

        //    connection?.Close(System.TimeSpan.FromSeconds(5));
        //    return Task.CompletedTask;
        //}

        private class MultipleEndpointResolver : DefaultEndpointResolver
        {
            public MultipleEndpointResolver(IRabbitMqOptions options) : base(AmqpTcpEndpoint.ParseMultiple(options.Server)) { }
        }
    }
}
