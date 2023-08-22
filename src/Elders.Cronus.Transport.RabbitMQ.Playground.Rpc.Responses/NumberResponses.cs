using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Transport.RabbitMQ.RpcAPI;
using System.Runtime.Serialization;

namespace Elders.Cronus.Transport.RabbitMQ.Playground.Rpc.Responses
{
    [DataContract(Namespace = "rmqresponses", Name = "4413d9ce-37c2-4638-b2ac-a1d60b119694")]
    public class NumberRequest : RpcRequest<NumberResponse>
    {
        [DataMember(Order = 1)]
        public int Number { get; set; }
    }

    [DataContract(Name = "edf9cefe-f42d-4b5d-ba0f-6360484c377e")]
    public class NumberResponse
    {
        [DataMember(Order = 1)]
        public int DoubleNumber { get; set; }
    }

    public class NumbersResponser :
        IRequestHandler<NumberRequest, RpcResponse<NumberResponse>>
    {
        private readonly CronusContext cronusContext;

        /*public NumbersResponser(CronusContext cronusContext)
        {
            this.cronusContext = cronusContext;
        }*/

        public async Task<RpcResponse<NumberResponse>> HandleAsync(NumberRequest request)
        {
            return request.Respond(new NumberResponse()
            {
                DoubleNumber = request.Number * 2
            });
        }
    }
}
