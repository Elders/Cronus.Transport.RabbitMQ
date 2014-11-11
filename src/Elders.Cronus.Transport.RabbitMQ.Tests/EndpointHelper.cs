using Elders.Cronus.Pipeline;
using Elders.Cronus.Pipeline.Transport.RabbitMQ;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;
using Elders.Cronus.Pipeline.Transport.RabbitMQ.Strategy;
using Machine.Specifications;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ.Tests
{
    internal static class EndpointHelper_FOR_TESTS_ONLY
    {
        public static void DeleteEndpointAndPipelines(this EndpointDefinition endpoint)
        {
            var client = new RestSharp.RestClient("http://localhost:15672/api");

            var request = new RestSharp.RestRequest("/queues/%2f/" + endpoint.EndpointName, RestSharp.Method.DELETE);
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));

            var response = client.Execute(request);

            request = new RestSharp.RestRequest("/exchanges/%2f/" + endpoint.PipelineName, RestSharp.Method.DELETE);
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));

            var response2 = client.Execute(request);
        }
    }

    internal class Binding
    {
        public string Routing_Key { get; set; }

        public string Destination { get; set; }

        public string Source { get; set; }

        public Dictionary<string, string> Arguments { get; set; }

        public Dictionary<string, object> Headers
        {
            get
            {
                var dc = new Dictionary<string, object>();
                foreach (var item in Arguments)
                {
                    dc.Add(item.Key, item.Value);
                }
                return dc;
            }
        }
    }

    internal class Queue
    {
        public string Name { get; set; }

        public Dictionary<string, string> Arguments { get; set; }
    }

    internal class Exchange
    {
        public string Name { get; set; }
    }
}