using Elders.Cronus.Pipeline.Transport.RabbitMQ.Config;

namespace Elders.Cronus.Pipeline.Hosts
{
    public class RabbitMqCronusHost : CronusHost
    {
        public RabbitMqCronusHost(CronusConfiguration configuration)
            : base(configuration)
        {

        }

        protected override void OnHostStop()
        {
          
        }
    }
}