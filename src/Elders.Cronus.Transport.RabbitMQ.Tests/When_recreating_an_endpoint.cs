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
    public class When_recreating_an_endpoint
    {
        Establish context = () =>
        {
            var pipelineConvention = new RabbitMqPipelinePerApplication();
            var endpointConvention = new RabbitMqEndpointPerBoundedContext(pipelineConvention);
            transport = new RabbitMqTransport("localhost", 5672, 15672, ConnectionFactory.DefaultUser, ConnectionFactory.DefaultPass, ConnectionFactory.DefaultVHost, pipelineConvention, endpointConvention);
            oldEndpointDefinition = new Pipeline.EndpointDefinition("When_Creating_An_Endpoint_Exchange", "When_Creating_An_Endpoint_Queue", new Dictionary<string, object>() { { "Header 1", "Value 1" } }, "testRoutingKey");
            transport.EndpointFactory.CreateEndpoint(oldEndpointDefinition);
            newEndpointDefinition = new Pipeline.EndpointDefinition("When_Creating_An_Endpoint_Exchange", "When_Creating_An_Endpoint_Queue", new Dictionary<string, object>() { { "New Header 1", "New Value 1" } }, "newTestRoutingKey");

            restClient = new RestClient("http://localhost:15672/api");

            getQueueRequest = new RestRequest("/queues/%2f/" + newEndpointDefinition.EndpointName, Method.GET);
            getQueueRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
            getExchangeRequest = new RestRequest("/exchanges/%2f/" + newEndpointDefinition.PipelineName, Method.GET);
            getExchangeRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
            getBindingsRequest = new RestRequest("/queues/%2f/" + newEndpointDefinition.EndpointName + "/bindings", Method.GET);
            getBindingsRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        };

        Because of = () => transport.EndpointFactory.CreateEndpoint(newEndpointDefinition);

        It should_have_queue_in_rabbit_mq = () =>
        {
            restClient.Execute<List<Queue>>(getQueueRequest).Data.Where(x => x.Name == newEndpointDefinition.EndpointName).SingleOrDefault().ShouldNotBeNull();
        };

        It should_have_exchange_in_rabbit_mq = () =>
        {
            restClient.Execute<List<Exchange>>(getExchangeRequest).Data.Where(x => x.Name == newEndpointDefinition.PipelineName).SingleOrDefault().ShouldNotBeNull();
        };

        It should_have_queue_with_the_new_routing_key_in_rabbit_mq = () =>
        {
            restClient.Execute<List<Binding>>(getBindingsRequest).Data.Where(x => x.Destination == newEndpointDefinition.EndpointName && x.Routing_Key == "newTestRoutingKey").SingleOrDefault().ShouldNotBeNull();
        };

        It should_have_queue_with_the_new_headers_only = () =>
        {
            restClient.Execute<List<Binding>>(getBindingsRequest).Data.Where(x => x.Destination == newEndpointDefinition.EndpointName && x.Arguments.Any(y => y.Key == "New Header 1" && y.Value == "New Value 1")).SingleOrDefault().ShouldNotBeNull();
        };

        Cleanup cleanup = () =>
        {
            newEndpointDefinition.DeleteEndpointAndPipelines();
        };

        static RabbitMqTransport transport;
        static EndpointDefinition oldEndpointDefinition;
        static EndpointDefinition newEndpointDefinition;
        static RestClient restClient;
        static RestRequest getBindingsRequest;
        static RestRequest getQueueRequest;
        static RestRequest getExchangeRequest;
    }
}
