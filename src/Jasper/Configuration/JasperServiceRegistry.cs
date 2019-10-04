﻿using System;
using System.Linq;
using Jasper.Conneg;
using Jasper.Http.ContentHandling;
using Jasper.Http.Model;
using Jasper.Http.Routing;
using Jasper.Messaging;
using Jasper.Messaging.Durability;
using Jasper.Messaging.Logging;
using Jasper.Messaging.Model;
using Jasper.Messaging.Runtime.Serializers;
using Jasper.Messaging.Sagas;
using Jasper.Messaging.Scheduled;
using Jasper.Messaging.Transports;
using Jasper.Messaging.Transports.Stub;
using Jasper.Messaging.Transports.Tcp;
using Lamar;
using Lamar.IoC.Instances;
using LamarCodeGeneration.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;

namespace Jasper.Configuration
{
    internal class JasperServiceRegistry : ServiceRegistry
    {
        public JasperServiceRegistry(JasperRegistry parent)
        {
            For<IMetrics>().Use<NulloMetrics>();
            For<IHostedService>().Use<MetricsCollector>();

            Policies.Add(new HandlerAndRoutePolicy(parent.JasperHttpRoutes.Routes, parent.Messaging.Graph));

            this.AddLogging();

            For<IMessageLogger>().Use<MessageLogger>().Singleton();
            For<ITransportLogger>().Use<TransportLogger>().Singleton();

            For<IMessageSerializer>().Use<EnvelopeReaderWriter>();
            For<IMessageDeserializer>().Use<EnvelopeReaderWriter>();


            this.AddSingleton(parent.CodeGeneration);

            For<IHostedService>().Use<BackPressureAgent>();
            For<IHostedService>().Use<DurabilityAgent>();

            conneg(parent);
            messaging(parent);

            aspnetcore(parent);
        }

        private void aspnetcore(JasperRegistry parent)
        {
            this.AddSingleton<ConnegRules>();

            this.AddScoped<IHttpContextAccessor>(x => new HttpContextAccessor());
            this.AddSingleton(parent.JasperHttpRoutes.Routes);
            ForSingletonOf<IUrlRegistry>().Use<UrlGraph>();

            this.AddSingleton<IServiceProviderFactory<IServiceCollection>>(new DefaultServiceProviderFactory());


        }

        private void conneg(JasperRegistry parent)
        {
            this.AddOptions();

            var forwarding = new Forwarders();
            For<Forwarders>().Use(forwarding);

            Scan(_ =>
            {
                _.Assembly(parent.ApplicationAssembly);
                _.AddAllTypesOf<IMessageSerializer>();
                _.AddAllTypesOf<IMessageDeserializer>();
                _.With(new ForwardingRegistration(forwarding));
            });
        }

        private void messaging(JasperRegistry parent)
        {
            ForSingletonOf<MessagingSerializationGraph>().Use<MessagingSerializationGraph>();

            For<IEnvelopePersistence>().Use<NulloEnvelopePersistence>();
            this.AddSingleton<InMemorySagaPersistor>();

            this.AddSingleton(parent.Messaging.Graph);
            this.AddSingleton<ISubscriberGraph>(parent.Messaging.Subscribers);
            this.AddSingleton<ILoopbackWorkerSender>(parent.Messaging.LocalWorker);


            For<ITransport>()
                .Use<LoopbackTransport>();

            For<ITransport>()
                .Use<TcpTransport>();

            For<ITransport>()
                .Use<StubTransport>().Singleton();

            ForSingletonOf<IMessagingRoot>().Use<MessagingRoot>();

            ForSingletonOf<ObjectPoolProvider>().Use(new DefaultObjectPoolProvider());

            MessagingRootService(x => x.Workers);
            MessagingRootService(x => x.Pipeline);

            MessagingRootService(x => x.Router);
            MessagingRootService(x => x.ScheduledJobs);

            For<IMessageContext>().Use<MessageContext>();



            For<IMessageContext>().Use(c => c.GetInstance<IMessagingRoot>().NewContext());
            For<ICommandBus>().Use(c => c.GetInstance<IMessagingRoot>().NewContext());
            For<IMessagePublisher>().Use(c => c.GetInstance<IMessagingRoot>().NewContext());

            ForSingletonOf<ITransportLogger>().Use<TransportLogger>();

        }

        public void MessagingRootService<T>(Func<IMessagingRoot, T> expression) where T : class
        {
            For<T>().Use(c => expression(c.GetInstance<IMessagingRoot>())).Singleton();
        }
    }

    internal class HandlerAndRoutePolicy : IFamilyPolicy
    {
        private readonly RouteGraph _routes;
        private readonly HandlerGraph _handlers;

        public HandlerAndRoutePolicy(RouteGraph routes, HandlerGraph handlers)
        {
            _routes = routes;
            _handlers = handlers;
        }

        private bool matches(Type type)
        {
            if (_routes.Any(x => x.Action.HandlerType == type)) return true;


            var handlerTypes = _handlers.Chains.SelectMany(x => x.Handlers)
                .Select(x => x.HandlerType);

            return handlerTypes.Contains(type);
        }

        public ServiceFamily Build(Type type, ServiceGraph serviceGraph)
        {
            if (type.IsConcrete() && matches(type))
            {
                var instance = new ConstructorInstance(type, type, ServiceLifetime.Scoped);
                return new ServiceFamily(type, new IDecoratorPolicy[0], instance);
            }

            return null;
        }
    }
}
