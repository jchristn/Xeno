namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Xeno.Core.Models;

    /// <summary>
    /// Bounded in-memory ledger queue.
    /// </summary>
    public class LedgerQueue
    {
        #region Public-Members

        /// <summary>
        /// Queue depth.
        /// </summary>
        public int Count
        {
            get { return _Queue.Count; }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentQueue<LedgerEvent> _Queue = new ConcurrentQueue<LedgerEvent>();
        private readonly int _MaxDepth;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the queue.
        /// </summary>
        /// <param name="maxDepth">Maximum queue depth.</param>
        public LedgerQueue(int maxDepth)
        {
            _MaxDepth = Math.Clamp(maxDepth, 1, 100000);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Try to enqueue an event.
        /// </summary>
        /// <param name="ledgerEvent">Ledger event.</param>
        /// <returns>True if the event was queued.</returns>
        public bool TryEnqueue(LedgerEvent ledgerEvent)
        {
            if (ledgerEvent == null) throw new ArgumentNullException(nameof(ledgerEvent));
            if (_Queue.Count >= _MaxDepth) return false;
            _Queue.Enqueue(ledgerEvent);
            return true;
        }

        /// <summary>
        /// Dequeue up to a maximum number of events.
        /// </summary>
        /// <param name="maxCount">Maximum event count.</param>
        /// <returns>Dequeued events.</returns>
        public List<LedgerEvent> DequeueBatch(int maxCount)
        {
            int limit = Math.Clamp(maxCount, 1, 1000);
            List<LedgerEvent> events = new List<LedgerEvent>();
            while (events.Count < limit && _Queue.TryDequeue(out LedgerEvent? ledgerEvent))
            {
                events.Add(ledgerEvent);
            }

            return events;
        }

        /// <summary>
        /// Requeue an event for retry.
        /// </summary>
        /// <param name="ledgerEvent">Ledger event.</param>
        public void Requeue(LedgerEvent ledgerEvent)
        {
            if (ledgerEvent == null) throw new ArgumentNullException(nameof(ledgerEvent));
            _Queue.Enqueue(ledgerEvent);
        }

        /// <summary>
        /// Get the oldest queued event age in milliseconds.
        /// </summary>
        /// <returns>Oldest event age, if any.</returns>
        public long? GetOldestAgeMs()
        {
            if (!_Queue.TryPeek(out LedgerEvent? ledgerEvent)) return null;
            return Math.Max(0, (long)(DateTime.UtcNow - ledgerEvent.CreatedUtc).TotalMilliseconds);
        }

        #endregion
    }
}
