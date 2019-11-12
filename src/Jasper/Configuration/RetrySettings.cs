using System;
using Baseline.Dates;

namespace Jasper.Configuration
{
    public class RetrySettings
    {
        /// <summary>
        /// Duration of time to wait before attempting to "ping" a transport
        /// in an attempt to resume a broken sending circuit
        /// </summary>
        public TimeSpan Cooldown { get; set; } = 1.Seconds();

        /// <summary>
        /// How many times outgoing message sending can fail before tripping
        /// off the circuit breaker functionality. Applies to all transport types
        /// </summary>
        public int FailuresBeforeCircuitBreaks { get; set; } = 3;

        /// <summary>
        /// Caps the number of envelopes held in memory for outgoing retries
        /// if an outgoing transport fails.
        /// </summary>
        public int MaximumEnvelopeRetryStorage { get; set; } = 100;

        /// <summary>
        /// Governs the page size for how many persisted incoming or outgoing messages
        /// will be loaded at one time for attempted retries
        /// </summary>
        public int RecoveryBatchSize { get; set; } = 100;

        /// <summary>
        /// How frequently Jasper will attempt to reassign incoming or outgoing
        /// persisted methods from nodes that are detected to be offline
        /// </summary>
        public TimeSpan NodeReassignmentPollingTime { get; set; } = 1.Minutes();

        /// <summary>
        /// When should the first execution of the node reassignment job
        /// execute after application startup.
        /// </summary>
        public TimeSpan FirstNodeReassignmentExecution { get; set; } = 0.Seconds();
    }
}
