using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elders.Cronus.Transport.RabbitMQ.Internal
{
    ///<summary>A thread-safe shared queue implementation.</summary>
    internal sealed class SharedQueue<T> : IEnumerable<T>
    {
        private bool m_isOpen = true;
        private Queue<T> m_queue = new Queue<T>();
        private Queue<TaskCompletionSource<T>> m_waiting = new Queue<TaskCompletionSource<T>>();

        public void Close()
        {
            lock (m_queue)
            {
                m_isOpen = false;
                Monitor.PulseAll(m_queue);

                // let all waiting tasks know we just closed by passing them an exception
                if (m_waiting.Count > 0)
                {
                    try
                    {
                        EnsureIsOpen();
                    }
                    catch (Exception ex)
                    {
                        foreach (var tcs in m_waiting)
                        {
                            tcs.TrySetException(ex);
                        }
                    }
                }
            }
        }

        public T Dequeue()
        {
            lock (m_queue)
            {
                while (m_queue.Count == 0)
                {
                    EnsureIsOpen();
                    Monitor.Wait(m_queue);
                }
                return m_queue.Dequeue();
            }
        }

        /// <summary>
        /// Asynchronously retrieves the first item from the queue.
        /// </summary>
        public Task<T> DequeueAsync()
        {
            lock (m_queue)
            {
                EnsureIsOpen();
                if (m_queue.Count > 0)
                {
                    return Task.FromResult(Dequeue());
                }
                else
                {
                    var tcs = new TaskCompletionSource<T>();
                    m_waiting.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        public bool Dequeue(int millisecondsTimeout, out T result)
        {
            if (millisecondsTimeout == Timeout.Infinite)
            {
                result = Dequeue();
                return true;
            }

            DateTime startTime = DateTime.Now;
            lock (m_queue)
            {
                while (m_queue.Count == 0)
                {
                    EnsureIsOpen();
                    var elapsedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                    int remainingTime = millisecondsTimeout - elapsedTime;
                    if (remainingTime <= 0)
                    {
                        result = default;
                        return false;
                    }

                    Monitor.Wait(m_queue, remainingTime);
                }

                result = m_queue.Dequeue();
                return true;
            }
        }

        public T DequeueNoWait(T defaultValue)
        {
            lock (m_queue)
            {
                if (m_queue.Count == 0)
                {
                    EnsureIsOpen();
                    return defaultValue;
                }
                else
                {
                    return m_queue.Dequeue();
                }
            }
        }

        public void Enqueue(T o)
        {
            lock (m_queue)
            {
                EnsureIsOpen();

                while (m_waiting.Count > 0)
                {
                    var tcs = m_waiting.Dequeue();
                    if (tcs != null && tcs.TrySetResult(o))
                    {
                        // We successfully set a task return result, so
                        // no need to Enqueue or Monitor.Pulse
                        return;
                    }
                }

                m_queue.Enqueue(o);
                Monitor.Pulse(m_queue);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SharedQueueEnumerator<T>(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new SharedQueueEnumerator<T>(this);
        }

        private void EnsureIsOpen()
        {
            if (!m_isOpen)
            {
                throw new EndOfStreamException("SharedQueue closed");
            }
        }
    }

    internal struct SharedQueueEnumerator<T> : IEnumerator<T>
    {
        private readonly SharedQueue<T> m_queue;
        private T m_current;

        internal SharedQueueEnumerator(SharedQueue<T> queue)
        {
            m_queue = queue;
            m_current = default;
        }

        object IEnumerator.Current
        {
            get
            {
                if (m_current == null)
                {
                    throw new InvalidOperationException();
                }
                return m_current;
            }
        }

        T IEnumerator<T>.Current
        {
            get
            {
                if (m_current == null)
                {
                    throw new InvalidOperationException();
                }
                return m_current;
            }
        }

        public void Dispose()
        {
        }

        bool IEnumerator.MoveNext()
        {
            try
            {
                m_current = m_queue.Dequeue();
                return true;
            }
            catch (EndOfStreamException)
            {
                m_current = default;
                return false;
            }
        }

        void IEnumerator.Reset()
        {
            throw new InvalidOperationException("SharedQueue.Reset() does not make sense");
        }
    }
}
