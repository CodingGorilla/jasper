﻿using System;
using Baseline;
using Jasper.Configuration;
using Jasper.Http.ContentHandling;
using Jasper.Http.Model;
using Jasper.Messaging.Model;
using LamarCodeGeneration;
using LamarCompiler;
using Oakton;
using Oakton.AspNetCore;

namespace Jasper.CommandLine
{
    [Description("Display or export the runtime, generated code in this application")]
    public class CodeCommand : OaktonCommand<CodeInput>
    {

        public override bool Execute(CodeInput input)
        {
            Console.WriteLine("Generating a preview of the generated code, this might take a bit...");
            Console.WriteLine();
            Console.WriteLine();

            using (var runtime = new JasperRuntime(input.BuildHost()))
            {
                var rules = runtime.Get<JasperGenerationRules>();
                var generatedAssembly = new GeneratedAssembly(rules);

                if (input.MatchFlag == CodeMatch.all || input.MatchFlag == CodeMatch.messages)
                {
                    var handlers = runtime.Get<HandlerGraph>();
                    foreach (var handler in handlers.Chains) handler.AssembleType(generatedAssembly, rules);
                }

                if (input.MatchFlag == CodeMatch.all || input.MatchFlag == CodeMatch.routes)
                {
                    var connegRules = runtime.Get<ConnegRules>();
                    var routes = runtime.Get<RouteGraph>();

                    foreach (var route in routes) route.AssemblyType(generatedAssembly, connegRules, rules);
                }

                var text = generatedAssembly.GenerateCode(runtime.Container.CreateServiceVariableSource());

                Console.WriteLine(text);

                if (input.FileFlag.IsNotEmpty())
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine($"Writing file {input.FileFlag.ToFullPath()}");
                    new FileSystem().WriteStringToFile(input.FileFlag, text);
                }
            }

            return true;
        }
    }

    public enum CodeMatch
    {
        all,
        messages,
        routes
    }

    public class CodeInput : AspNetCoreInput
    {
        public CodeMatch MatchFlag { get; set; } = CodeMatch.all;

        [System.ComponentModel.Description("Optional file name to export the contents")]
        public string FileFlag { get; set; }
    }
}
