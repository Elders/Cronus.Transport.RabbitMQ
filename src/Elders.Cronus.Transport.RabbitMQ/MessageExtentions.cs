using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public static class MessageExtentions
    {
        public static long GetPublishDelay(this CronusMessage message)
        {
            string publishAt = "0";
            if (message.Headers.TryGetValue(MessageHeader.PublishTimestamp, out publishAt))
            {
                return (long)(DateTime.FromFileTimeUtc(long.Parse(publishAt)) - DateTime.UtcNow).TotalMilliseconds;
            }
            return 0;
        }
    }
}
