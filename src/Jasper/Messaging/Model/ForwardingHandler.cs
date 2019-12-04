﻿using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Jasper.Conneg;

namespace Jasper.Messaging.Model
{
    internal class ForwardingHandler<T, TDestination> : MessageHandler where T : IForwardsTo<TDestination>
    {
        private readonly HandlerGraph _graph;

        public ForwardingHandler(HandlerGraph graph)
        {
            _graph = graph;
            Chain = new HandlerChain(typeof(T));
        }

        public override Task Handle(IMessageContext context, CancellationToken cancellation)
        {
            var innerMessage = context.Envelope.Message.As<T>();
            context.Envelope.Message = innerMessage.Transform();

            var inner = _graph.HandlerFor(typeof(TDestination));

            return inner.Handle(context, cancellation);
        }
    }
}
