﻿using Jasper;
using Jasper.Configuration;

namespace Module1
{
    public class Module1Extension : IJasperExtension
    {
        public static JasperOptions Options { get; set; }

        public void Configure(JasperOptions options)
        {
            Options = options;

            options.Settings.Alter<ModuleSettings>(_ =>
            {
                _.From = "Module1";
                _.Count = 100;
            });

            options.Services.For<IModuleService>().Use<ServiceFromModule>();
        }
    }

    public interface IModuleService
    {
    }

    public class ModuleSettings
    {
        public string From { get; set; } = "Default";
        public int Count { get; set; }
    }

    public class ServiceFromModule : IModuleService
    {
    }
}
