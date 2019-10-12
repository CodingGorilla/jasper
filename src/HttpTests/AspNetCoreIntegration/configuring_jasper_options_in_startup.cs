using System.Collections.Generic;
using System.Linq;
using Jasper;
using Jasper.Configuration;
using Jasper.Util;
using JasperHttp;
using Lamar;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HttpTests.AspNetCoreIntegration
{
    public class configuring_jasper_options_in_startup
    {
        [Fact]
        public void bootstrap_with_WebHostBuilder()
        {
            var configuration = new Dictionary<string, string>
                {{"name", "MyJasperApp"}, {"TestingListener", "tcp://localhost:4532"}};

            var builder = new WebHostBuilder();
            using (var host = builder
                .ConfigureAppConfiguration(c => c.AddInMemoryCollection(configuration))
                .UseServer(new NulloServer())
                .UseEnvironment("Testing")
                .UseStartup<Startup>()
                .UseJasper()
                .Start())
            {
                var options = host.Services.GetRequiredService<JasperOptions>();

                options.ServiceName.ShouldBe("MyJasperApp");
                options.Listeners.Single(x => x.Scheme == "tcp").ShouldBe("tcp://localhost:4532".ToUri());
            }



        }





        public class Startup
        {
            public void ConfigureContainer(ServiceRegistry services)
            {
                // do any kind of Lamar service registrations
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env, IConfiguration configuration, JasperOptions jasper)
            {
                jasper.ServiceName = configuration["name"];

                var listener = $"{env.EnvironmentName}Listener";

                jasper.ListenForMessagesFrom(configuration[listener]);
            }
        }
    }
}
