using System.Diagnostics;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using YamlDotNet.Serialization;
using System.IO;

namespace Azure.Tenant.Automation
{
    public class Program
    {
        private readonly Dictionary<string, string> _tagKeysNeedingUpdated;

        public Program()
        {
            var deserializer = new DeserializerBuilder().Build();
            var yamlString = File.ReadAllText("tags.yaml");
            _tagKeysNeedingUpdated = deserializer.Deserialize<Dictionary<string, string>>(yamlString);
        }

        public static async Task Main()
        {
            ArmClient azure = new(new DefaultAzureCredential());
            const string _targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
            const string _targetSubscription = "";
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
                    if(subscription.State.ToString() != "Enabled")
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

                if (subscriptionTags?.Count > 0)
                {
                    var updatedTags = _program.UpdateTags(subscriptionTags, subscription.Data.DisplayName, "subscription");

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

                        if (resourceGroupTags?.Count > 0)
                        {
                            try
                            {
                                var updatedTags = _program.UpdateTags(resourceGroupTags, resourceGroupName, "resource group");
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

                            if (resourceTags?.Count > 0)
                            {
                                try
                                {
                                    var updatedTags = _program.UpdateTags(resourceTags, resourceName, resourceType);
                                    resource.SetTagsAsync(updatedTags).Wait();
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
        }

        public Dictionary<string, string> UpdateTags(IDictionary<string, string> currentTags, string itemName, string resourceType = "")
        {
            var originalTags = new Dictionary<string, string>(currentTags);
            Dictionary<string, string> tagsToApply = new();

            var markedTagsForUpdate = currentTags
                    .Where(tag => _tagKeysNeedingUpdated.ContainsKey(tag.Key))
                    .ToList();

            if (markedTagsForUpdate?.Count > 0)
            {
                tagsToApply = markedTagsForUpdate.ToDictionary(x => x.Key, x => x.Value);
                markedTagsForUpdate.ForEach(tag =>
                {
                    string resourceMessage = !String.IsNullOrEmpty(resourceType) ? $"{resourceType}:" : "item:";
                    Console.WriteLine($"Changing tag {tag.Key} to {_tagKeysNeedingUpdated[tag.Key]} for {resourceMessage} {itemName}");
                    // Add new tag key, retaining existing value
                    tagsToApply[_tagKeysNeedingUpdated[tag.Key]] = tag.Value;
                    // Remove old tag from both dictionaries
                    tagsToApply.Remove(tag.Key);
                    originalTags.Remove(tag.Key);
                });

            }
            else
            {
                Console.WriteLine($"No tags requiring updating on {itemName}.");
            }

            if (tagsToApply.Count > 0 || originalTags.Count > 0)
            {
                // Recombine existing tags with updated tags
                foreach (var tag in originalTags)
                {
                    if (!tagsToApply.ContainsKey(tag.Key))
                    {
                        tagsToApply.Add(tag.Key, tag.Value);
                    }
                }
            }
            else
            {
                Console.WriteLine($"No updated tags to recombine for item {itemName}. Check if the tags were properly updated.");
            }

            return tagsToApply;
        }
    }
}