using System;
using System.Threading.Tasks;
using Jasper.Logging;
using Jasper.Persistence.Durability;

namespace Jasper
{
    public interface IAdvancedMessagingActions
    {

        /// <summary>
        ///     Current message logger
        /// </summary>
        IMessageLogger Logger { get; }

        IEnvelopePersistence Persistence { get; }

        /// <summary>
        ///     Send a failure acknowledgement back to the original
        ///     sending service
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task SendFailureAcknowledgement(string message);

        /// <summary>
        ///     Dumps any outstanding, cascading messages and requeues the current envelope (if any) into the local worker queue
        /// </summary>
        /// <returns></returns>
        Task Retry();

        /// <summary>
        ///     Sends an acknowledgement back to the original sender
        /// </summary>
        /// <returns></returns>
        Task SendAcknowledgement();

        /// <summary>
        ///     Send a message envelope. Gives you complete power over how the message
        ///     is delivered
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        Task<Guid> SendEnvelope(Envelope envelope);

        /// <summary>
        ///     Enqueue a cascading message to the outstanding context transaction
        ///     Can be either the message itself, any kind of ISendMyself object,
        ///     or an IEnumerable<object>
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task EnqueueCascading(object message);
    }
}
