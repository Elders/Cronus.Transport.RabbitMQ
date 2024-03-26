using System;

namespace Elders.Cronus.Transport.RabbitMQ
{
    internal static class MessageExtentions
    {
        private const long TtlTreasholdMilliseconds = 30_000; // https://learn.microsoft.com/en-us/azure/virtual-machines/linux/time-sync
        private const string NoDelay = "0";

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
        private static string GetTtlMillisecondsFromHeader(this CronusMessage message)
        {
            string ttl = string.Empty;
            message.Headers.TryGetValue(MessageHeader.TTL, out ttl);

            return ttl;
        }

        /// <summary>
        /// Gets the <see cref="CronusMessage"/> TTL. Because there are 2 ways to set a message delay, <see cref="GetPublishDelayMilliseconds(CronusMessage)"/> is with higher priority than
        /// <see cref="GetTtlMillisecondsFromHeader(CronusMessage)"/>
        /// </summary>
        /// <param name="message">The <see cref="CronusMessage"/>.</param>
        /// <returns>Returns the TTL in milliseconds.</returns>
        internal static string GetTtlMilliseconds(this CronusMessage message)
        {
            long ttl = GetPublishDelayMilliseconds(message);
            if (ttl < TtlTreasholdMilliseconds)
            {
                string ttlFromHeader = GetTtlMillisecondsFromHeader(message);
                if (string.IsNullOrEmpty(ttlFromHeader))
                    return NoDelay;

                ttl = long.Parse(ttlFromHeader);
                if (ttl < TtlTreasholdMilliseconds)
                    return NoDelay;
            }

            return ttl.ToString();
        }
    }
}
