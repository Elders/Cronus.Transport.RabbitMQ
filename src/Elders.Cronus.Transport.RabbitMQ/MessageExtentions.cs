using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    internal static class MessageExtentions
    {
        /// <summary>
        /// Gets the <see cref="CronusMessage"/> TTL calculate => Utc.Now - MessageHeaders[publish_timestamp].
        /// </summary>
        /// <param name="message">The <see cref="CronusMessage"/>.</param>
        /// <returns>Returns the TTL in milliseconds.</returns>
        private static long GetPublishDelayMilliseconds(this CronusMessage message)
        {
            if (message.Headers.TryGetValue(MessageHeader.PublishTimestamp, out string publishAt))
            {
                return (long)(DateTime.FromFileTimeUtc(long.Parse(publishAt)) - DateTime.UtcNow).TotalMilliseconds;
            }

            return 0;
        }

        /// <summary>
        /// Gets the <see cref="CronusMessage"/> TTL from the MessageHeaders[ttl].
        /// </summary>
        /// <param name="message">The <see cref="CronusMessage"/>.</param>
        /// <returns>Returns the TTL in milliseconds.</returns>
        private static string GetTtlMilliseconds(this CronusMessage message)
        {
            string ttl = string.Empty;
            message.Headers.TryGetValue(MessageHeader.TTL, out ttl);

            return ttl;
        }

        /// <summary>
        /// Gets the <see cref="CronusMessage"/> TTL. Because there are 2 ways to set a message delay, <see cref="GetPublishDelayMilliseconds(CronusMessage)"/> is with higher priority than
        /// <see cref="GetTtlMilliseconds(CronusMessage)"/>
        /// </summary>
        /// <param name="message">The <see cref="CronusMessage"/>.</param>
        /// <returns>Returns the TTL in milliseconds.</returns>
        internal static string GetTtl(this CronusMessage message)
        {
            long ttl = GetPublishDelayMilliseconds(message);
            if (ttl <= 0)
                return GetTtlMilliseconds(message);

            return ttl.ToString();
        }
    }
}
