// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.SemanticKernel.Handlebars;

public sealed class HandlebarsPlanner
{
    private readonly IKernel Kernel;

    private readonly HandlebarsPlannerConfiguration Configuration;

    public HandlebarsPlanner(IKernel kernel, HandlebarsPlannerConfiguration? configuration = default)
    {
        this.Kernel = kernel;
        this.Configuration = configuration ?? new HandlebarsPlannerConfiguration();
    }
    public async Task<HandlebarsPlan> CreatePlanAsync(string goal, CancellationToken cancellationToken = default)
    {
        string plannerTemplate;

        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("HandlebarPlanner.prompt.yaml")!)
        using (StreamReader reader = new(stream))
        {
            plannerTemplate = reader.ReadToEnd();
        }

        var plannerFunction = SemanticFunction.GetFunctionFromYamlContent(
            plannerTemplate,
            cancellationToken: cancellationToken
        );

        // Get functions
        var functions = ((Kernel)this.Kernel).GetFunctionViews().Where(f => 
        {
            var fullyQualifiedName = $"{f.PluginName}.{f.Name}";
            bool shouldInclude = false;
            if (Configuration.IncludedPlugins.Count == 0 && Configuration.IncludedFunctions.Count == 0)
            {
                shouldInclude = true;
            }
            
            if (Configuration.IncludedPlugins.Contains(f.PluginName))
            {
                shouldInclude = true;
            }
            if (Configuration.IncludedFunctions.Contains(fullyQualifiedName))
            {
                shouldInclude = true;
            }
            if (Configuration.ExcludedPlugins.Contains(f.PluginName))
            {
                shouldInclude = false;
            }
            if (Configuration.ExcludedFunctions.Contains(fullyQualifiedName))
            {
                shouldInclude = false;
            }

            return shouldInclude;
        }).ToList();

        // Generate the plan
        var result = await this.Kernel.RunAsync(
            plannerFunction,
            variables: new Dictionary<string, object>()
            {
                { "functions", functions},
                { "goal", goal }
            }
        );

        Match match = Regex.Match(result.GetValue<string>()!, @"```\s*(handlebars)?\s+(.*?)\s+```", RegexOptions.Singleline);

        if (!match.Success)
        {
            throw new Exception("Could not find the plan in the results");
        }
        
        var template = match.Groups[2].Value;

        return new HandlebarsPlan(Kernel, template);
    }
}

public class HandlebarsPlannerConfiguration
{
    public HandlebarsPlannerConfiguration(
        List<string>? includedPlugins = default,
        List<string>? excludedPlugins = default,
        List<string>? includedFunctions = default,
        List<string>? excludedFunctions = default
    )
    {
        IncludedPlugins = includedPlugins ?? new List<string>();
        ExcludedPlugins = excludedPlugins ?? new List<string>();
        IncludedFunctions = includedFunctions ?? new List<string>();
        ExcludedFunctions = excludedFunctions ?? new List<string>();
    }
    public List<string> IncludedPlugins { get; set; }
    public List<string> ExcludedPlugins { get; set; }
    public List<string> IncludedFunctions { get; set; }
    public List<string> ExcludedFunctions { get; set; }
}