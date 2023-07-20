using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Transport.RabbitMQ.RpcAPI;

namespace Elders.Cronus.Transport.RabbitMQ.Playground.Rpc.Requests
{
    public class Worker : BackgroundService
    {
        private readonly IServiceProvider provider;
        private readonly ICronusHost cronusHost;
        private readonly DefaultCronusContextFactory cronusContextFactory;
        private readonly ILogger<Worker> _logger;

        public Worker(IServiceProvider provider, ICronusHost cronusHost, DefaultCronusContextFactory cronusContextFactory, ILogger<Worker> logger)
        {
            this.provider = provider;
            this.cronusHost = cronusHost;
            this.cronusContextFactory = cronusContextFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting service...");

            cronusHost.Start();

            _logger.LogInformation("Service started!");

            using (var scrope = provider.CreateScope())
            {
                var context = cronusContextFactory.Create("elders", scrope.ServiceProvider);

                var rpc = scrope.ServiceProvider.GetRequiredService<IRpc<NumberRequest, RpcResponse<NumberResponse>>>();
                Console.WriteLine();
                List<Task> tasks = new List<Task>();
                for (int i = 1; i < int.MaxValue; i++)
                {
                    int scopedNumber = i;

                    var t = rpc.SendAsync(new NumberRequest(scopedNumber))
                        .ContinueWith(responseTask =>
                        {
                            if (responseTask.Result.IsSuccessful)
                            {
                                if (scopedNumber * 2 != responseTask.Result.Result.DoubleNumber)
                                    Console.WriteLine("maafaka");
                            }
                            else
                            {
                                Console.WriteLine(responseTask.Result.Error);
                            }
                        });
                    tasks.Add(t);
                    if (tasks.Count > 100)
                    {
                        var finished = await Task.WhenAny(tasks);
                        tasks.Remove(finished);
                    }
                }

                await Task.WhenAll(tasks);
            }
        }
    }
}
