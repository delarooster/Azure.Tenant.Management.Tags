using System.Diagnostics;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using YamlDotNet.Serialization;
using System.IO;
using System.Collections.Generic;
using System.Threading.Channels;

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
            ArmClient azure = new(new DefaultAzureCredential());
            const string _targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
            const string _targetSubscription = "cb70135b-a87f-47c4-adc2-9e172bc22f88"; // Dv-AD-Sdbx
            const string _targetResourceGroup = "";
            Program _program = new();

            var subscriptions = azure.GetSubscriptions().ToList();
            var tasks = subscriptions.Select(async sub =>
            {
                try
                {
                    Stopwatch stopWatch = new();
                    stopWatch.Start();

                    SubscriptionData? subscription = sub.Data;
                    if (subscription.State.ToString() != "Enabled")
                    {
                        Console.WriteLine($"Not Enabled: subscription {subscription.DisplayName}, skipping...");
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
                    await UpdateSubscriptionTags(sub);
                    await UpdateResourceGroupsTags(sub);

                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;

                    Console.WriteLine($"Finished updating {subscription.DisplayName} ({subscription.SubscriptionId}) in {ts.TotalSeconds} seconds.");
                }
                catch (Exception ex)
                {
                    // Log the error or handle it in any other way
                    Console.WriteLine($"An error occurred while updating the subscription {sub.Data.DisplayName}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);



            async Task UpdateSubscriptionTags(SubscriptionResource subscription)
            {
                Dictionary<string, string> subscriptionTags = new Dictionary<string, string>(subscription.Data.Tags);

                if (subscriptionTags.Any())
                {
                    var updatedTags = _program
                        .UpdateTagKeys(CreateAzureResource(subscriptionTags, subscription.Data.DisplayName, "subscription"))
                        .Pipe(tags => _program.UpdateTagValues(CreateAzureResource(tags, subscription.Data.DisplayName, "subscription")));

                    // Construct the TagResourceData object required to pass to subscription tag update
                    Tag tag = new();
                    foreach (var updatedTag in updatedTags)
                    {
                        tag.TagValues.Add(updatedTag.Key, updatedTag.Value);
                    }
                    TagResourceData tags = new(tag);
                    const Azure.WaitUntil wait = new();
                    await subscription.GetTagResource().CreateOrUpdateAsync(wait, tags);
                }
                else
                {
                    Console.WriteLine($"No tags on resource {subscription.Data.DisplayName}");
                }
            }

            async Task UpdateResourceGroupsTags(SubscriptionResource subscription)
            {
                try
                {
                    Parallel.ForEach(subscription.GetResourceGroups(), resourceGroup =>
                    {
                        string resourceGroupName = resourceGroup.Data.Name;
                        if (!String.IsNullOrEmpty(_targetResourceGroup) && resourceGroupName != _targetResourceGroup) return;

                        var resourceGroupTags = resourceGroup?.Data?.Tags;

                        if (resourceGroupTags.Any())
                        {
                            try
                            {
                                var updatedTags = _program
                                    .UpdateTagKeys(CreateAzureResource(resourceGroupTags, resourceGroupName, "resource group"))
                                    .Pipe(tags => _program.UpdateTagValues(CreateAzureResource(tags, resourceGroupName, "resource group")));

                                resourceGroup.SetTagsAsync(updatedTags).Wait();
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

                        try
                        {
                            UpdateResourcesTags(resourceGroup).Wait();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating tags for resources in resource group {resourceGroupName}: {ex.Message}");
                        }
                    });
                }
                catch (AggregateException ex)
                {
                    foreach (var innerEx in ex.InnerExceptions)
                    {
                        Console.WriteLine($"Error processing one or more resource groups: {innerEx.Message}");
                    }
                }

            }

            async Task UpdateResourcesTags(ResourceGroupResource resourceGroup)
            {
                var resources = resourceGroup.GetGenericResources();

                if (resources != null)
                {
                    try
                    {
                        Parallel.ForEach(resources, resource =>
                        {
                            var resourceTags = resource?.Data?.Tags;
                            var resourceName = resource?.Data?.Name;
                            var resourceType = resource?.Data?.ResourceType;

                            if (resourceTags != null && resourceTags.Any())
                            {
                                try
                                {
                                  var updatedTags = _program
                                    .UpdateTagKeys(CreateAzureResource(resourceTags, resourceName, resourceType))
                                    .Pipe(tags => _program.UpdateTagValues(CreateAzureResource(tags, resourceName, resourceType)));

                                  resource?.SetTagsAsync(updatedTags).Wait();
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
                        });
                    }
                    catch (AggregateException ex)
                    {
                        foreach (var innerEx in ex.InnerExceptions)
                        {
                            Console.WriteLine($"Error processing one or more resources: {innerEx.Message}");
                        }
                    }
                }
            }

            AzureResource CreateAzureResource(IDictionary<string, string> tags, string name, string type) => new AzureResource(tags, name, type);
            
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