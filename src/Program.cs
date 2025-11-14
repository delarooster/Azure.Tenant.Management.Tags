using System.Diagnostics;
using System.Linq;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using YamlDotNet.Serialization;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Azure.Tenant.Automation
{
    public class Program
    {
        private readonly Dictionary<string, string> _tagKeysToUpdate;
        private readonly Dictionary<string, string> _tagValuesToUpdate;

        public Program()
        {
            var deserializer = new DeserializerBuilder().Build();
            _tagKeysToUpdate = deserializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("tags.yaml"));
            _tagValuesToUpdate = deserializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("values.yaml"));
        }

        public static async Task Main()
        {
            // Build configuration from appsettings.json and environment variables
            // Environment variables take precedence (Docker-friendly)
            // Environment variables use double underscore (__) for nested keys: Azure__TargetTenant
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get configuration values - environment variables override appsettings.json
            var _targetTenant = configuration["Azure:TargetTenant"] 
                ?? throw new InvalidOperationException("TargetTenant must be configured in appsettings.json or Azure__TargetTenant environment variable");
            
            var _targetSubscription = configuration["Azure:TargetSubscription"] ?? string.Empty;
            var _targetResourceGroup = configuration["Azure:TargetResourceGroup"] ?? string.Empty;

            Program _program = new();

            ArmClient azure = new(new DefaultAzureCredential());
            var subscriptions = azure.GetSubscriptions().ToList();
            
            var tasks = subscriptions.Select(async sub =>
            {
                try
                {
                    Stopwatch stopWatch = Stopwatch.StartNew();

                    SubscriptionData? subscription = sub.Data;
                    if (subscription.State.ToString() != "Enabled")
                    {
                        Console.WriteLine($"Not Enabled: subscription {subscription.DisplayName}, skipping...");
                        return;
                    }
                    if (subscription.TenantId.ToString() != _targetTenant)
                    {
                        Console.WriteLine($"Outside target tenant: subscription {subscription.DisplayName}, skipping...");
                        return;
                    }
                    if (!String.IsNullOrEmpty(_targetSubscription) && subscription.SubscriptionId != _targetSubscription)
                    {
                        Console.WriteLine($"Skipping {subscription.DisplayName} - not target subscription.");
                        return;
                    }

                    Console.WriteLine($"Start updating {subscription.DisplayName} ({subscription.SubscriptionId})...");
                    await UpdateSubscriptionTags(sub, _program);
                    await UpdateResourceGroupsTags(sub, _program, _targetResourceGroup);

                    stopWatch.Stop();
                    Console.WriteLine($"Finished updating {subscription.DisplayName} ({subscription.SubscriptionId}) in {stopWatch.Elapsed.TotalSeconds:F2} seconds.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while updating the subscription {sub.Data.DisplayName}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);



            static async Task UpdateSubscriptionTags(SubscriptionResource subscription, Program program)
            {
                Dictionary<string, string> subscriptionTags = new Dictionary<string, string>(subscription.Data.Tags);

                if (subscriptionTags.Any())
                {
                    var updatedTags = program
                        .UpdateTagKeys(CreateAzureResource(subscriptionTags, subscription.Data.DisplayName, "subscription"))
                        .Pipe(tags => program.UpdateTagValues(CreateAzureResource(tags, subscription.Data.DisplayName, "subscription")));

                    // Construct the TagResourceData object required to pass to subscription tag update
                    Tag tag = new();
                    foreach (var updatedTag in updatedTags)
                    {
                        tag.TagValues.Add(updatedTag.Key, updatedTag.Value);
                    }
                    TagResourceData tagResourceData = new(tag);
                    await subscription.GetTagResource().CreateOrUpdateAsync(Azure.WaitUntil.Completed, tagResourceData);
                }
                else
                {
                    Console.WriteLine($"No tags on resource {subscription.Data.DisplayName}");
                }
            }

            static async Task UpdateResourceGroupsTags(SubscriptionResource subscription, Program program, string targetResourceGroup)
            {
                var resourceGroups = subscription.GetResourceGroups().ToList();
                var tasks = resourceGroups.Select(async resourceGroup =>
                {
                    try
                    {
                        string resourceGroupName = resourceGroup.Data.Name;
                        if (!String.IsNullOrEmpty(targetResourceGroup) && resourceGroupName != targetResourceGroup) 
                            return;

                        var resourceGroupTags = resourceGroup?.Data?.Tags;

                        if (resourceGroupTags != null && resourceGroupTags.Any())
                        {
                            try
                            {
                                var updatedTags = program
                                    .UpdateTagKeys(CreateAzureResource(resourceGroupTags, resourceGroupName, "resource group"))
                                    .Pipe(tags => program.UpdateTagValues(CreateAzureResource(tags, resourceGroupName, "resource group")));

                                if (resourceGroup != null)
                                {
                                    await resourceGroup.SetTagsAsync(updatedTags);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error updating tags for resource group {resourceGroupName}: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No tags on resource {resourceGroupName}");
                        }

                        if (resourceGroup != null)
                        {
                            try
                            {
                                await UpdateResourcesTags(resourceGroup, program);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error updating tags for resources in resource group {resourceGroupName}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing resource group {resourceGroup.Data.Name}: {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);
            }

            static async Task UpdateResourcesTags(ResourceGroupResource resourceGroup, Program program)
            {
                var resources = resourceGroup.GetGenericResources().ToList();

                if (resources != null && resources.Any())
                {
                    var tasks = resources.Select(async resource =>
                    {
                        try
                        {
                            var resourceTags = resource?.Data?.Tags;
                            var resourceName = resource?.Data?.Name;
                            var resourceType = resource?.Data?.ResourceType;

                            if (resourceTags != null && resourceTags.Any())
                            {
                                try
                                {
                                    var updatedTags = program
                                        .UpdateTagKeys(CreateAzureResource(resourceTags, resourceName ?? "unknown", resourceType?.ToString() ?? "unknown"))
                                        .Pipe(tags => program.UpdateTagValues(CreateAzureResource(tags, resourceName ?? "unknown", resourceType?.ToString() ?? "unknown")));

                                    if (resource != null)
                                    {
                                        await resource.SetTagsAsync(updatedTags);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"An error occurred while setting tags for the resource {resourceType} with name {resourceName}: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"No tags on resource {resourceName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing resource {resource?.Data?.Name}: {ex.Message}");
                        }
                    });

                    await Task.WhenAll(tasks);
                }
            }

            static AzureResource CreateAzureResource(IDictionary<string, string> tags, string name, string type) => new AzureResource(tags, name, type);
        }

        public IDictionary<string, string> UpdateTagKeys(AzureResource resources)
        {
            // Clone the original tags to results
            Dictionary<string, string> results = new Dictionary<string, string>(resources.CurrentTags);

            var keysForUpdate = resources.CurrentTags
                .Where(tag => _tagKeysToUpdate.ContainsKey(tag.Key))
                .ToList();

            if (keysForUpdate.Any())
            {
                foreach (var tag in keysForUpdate)
                {
                    string resourceMessage = !String.IsNullOrEmpty(resources.ResourceType)
                        ? $"{resources.ResourceType}:"
                        : "item:";

                    Console.WriteLine($"Changing tag key {tag.Key} to {_tagKeysToUpdate[tag.Key]} for {resourceMessage} {resources.ItemName}");

                    // Check if the new key doesn't already exist
                    if (!results.ContainsKey(_tagKeysToUpdate[tag.Key]))
                    {
                        // Add new key with the original value
                        results[_tagKeysToUpdate[tag.Key]] = tag.Value;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Key {_tagKeysToUpdate[tag.Key]} already exists. Skipping update for {tag.Key}.");
                    }
                }

                // Now, remove the old keys. Doing this in a separate loop ensures that we don't accidentally remove keys before their values are transferred.
                foreach (var tag in keysForUpdate)
                {
                    results.Remove(tag.Key);
                }
            }
            else
            {
                Console.WriteLine($"No tag keys requiring updating on {resources.ItemName}.");
            }

            return results;
        }

        public IDictionary<string, string> UpdateTagValues(AzureResource resources)
        {
            // Clone the original tags to results
            Dictionary<string, string> results = new Dictionary<string, string>(resources.CurrentTags);

            var valuesForUpdate = resources.CurrentTags
                .Where(tag => _tagValuesToUpdate.ContainsKey(tag.Value))
                .ToList();

            if (valuesForUpdate.Any())
            {
                foreach (var tag in valuesForUpdate)
                {
                    string resourceMessage = !String.IsNullOrEmpty(resources.ResourceType)
                        ? $"{resources.ResourceType}:"
                        : "item:";

                    Console.WriteLine($"Changing tag value {tag.Value} to {_tagValuesToUpdate[tag.Value]} for tag {tag.Key} on {resourceMessage} {resources.ItemName}");

                    // Update the value for the key directly in the results dictionary
                    results[tag.Key] = _tagValuesToUpdate[tag.Value];
                }
            }
            else
            {
                Console.WriteLine($"No tag values requiring updating on {resources.ItemName}.");
            }

            return results;
        }
    }
    public struct AzureResource
    {
        public AzureResource(IDictionary<string, string> currentTags, string itemName, string resourceType = "")
        {
            CurrentTags = currentTags;
            ItemName = itemName;
            ResourceType = resourceType;
        }
        public IDictionary<string, string> CurrentTags { get; }
        public string ItemName { get; }
        public string ResourceType { get; }
    }

    public static class FunctionalExtensions
    {
        public static TResult Pipe<T, TResult>(this T input, Func<T, TResult> func)
            => func(input);
    }
}