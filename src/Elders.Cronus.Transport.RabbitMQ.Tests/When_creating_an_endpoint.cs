using Elders.Cronus.Pipeline;
using Elders.Cronus.Pipeline.Transport.RabbitMQ;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy;
using Machine.Specifications;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestSharp;

namespace Elders.Cronus.Transport.RabbitMQ.Tests
{
    [Subject("Endpoint")]
    public class When_creating_an_endpoint
    {
        Establish context = () =>
        {
            var pipelineConvention = new RabbitMqPipelinePerApplication();
            var endpointConvention = new RabbitMqEndpointPerBoundedContext(pipelineConvention);
            transport = new RabbitMqTransport("docker-local.com", 5672, 15672, ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass, ConnectionFactory.DefaultVHost, pipelineConvention, endpointConvention);
            endpointDefinition = new Pipeline.EndpointDefinition("When_Creating_An_Endpoint_Exchange", "When_Creating_An_Endpoint_Queue", new Dictionary<string, object>() { { "Header 1", "Value 1" } }, "testRoutingKey");

            restClient = new RestClient("http://docker-local.com:15672/api");

            getQueueRequest = new RestRequest("/queues/%2f/" + endpointDefinition.EndpointName, Method.GET);
            getQueueRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
            getExchangeRequest = new RestRequest("/exchanges/%2f/" + endpointDefinition.PipelineName, Method.GET);
            getExchangeRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
            getBindingsRequest = new RestRequest("/queues/%2f/" + endpointDefinition.EndpointName + "/bindings", Method.GET);
            getBindingsRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        };

        Because of = () => transport.EndpointFactory.CreateEndpoint(endpointDefinition);

        It should_have_queue_in_rabbit_mq = () =>
            restClient.Execute<List<Queue>>(getQueueRequest).Data.Where(x => x.Name == endpointDefinition.EndpointName).SingleOrDefault().ShouldNotBeNull();

        It should_have_exchange_in_rabbit_mq = () =>
            restClient.Execute<List<Exchange>>(getExchangeRequest).Data.Where(x => x.Name == endpointDefinition.PipelineName).SingleOrDefault().ShouldNotBeNull();

        It should_have_queue_with_routing_key_in_rabbit_mq = () =>
            restClient.Execute<List<Binding>>(getBindingsRequest).Data.Where(x => x.Destination == endpointDefinition.EndpointName && x.Routing_Key == "testRoutingKey").SingleOrDefault().ShouldNotBeNull();

        It should_have_queue_with_headers_in_rabbit_mq = () =>
            restClient.Execute<List<Binding>>(getBindingsRequest).Data.Where(x => x.Destination == endpointDefinition.EndpointName && x.Arguments.Any(y => y.Key == "Header 1" && y.Value == "Value 1")).SingleOrDefault().ShouldNotBeNull();

        Cleanup cleanup = () => endpointDefinition.DeleteEndpointAndPipelines();

        static RabbitMqTransport transport;
        static EndpointDefinition endpointDefinition;
        static RestClient restClient;
        static RestRequest getBindingsRequest;
        static RestRequest getQueueRequest;
        static RestRequest getExchangeRequest;
    }
}