﻿using System.Diagnostics;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;

namespace Azure.Tenant.Automation
{
    public class Program
    {
        private readonly Dictionary<string, string> _tagKeysNeedingUpdated = new()
        {
            { "Client", "Customer" },
            { "Application", "Project" },
            { "App", "Project" }
        };

        public static async Task Main()
        {
            ArmClient azure = new(new DefaultAzureCredential());
            const string _targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
            const string _targetSubscription = "cb70135b-a87f-47c4-adc2-9e172bc22f88";
            const string _targetResourceGroup = "rg-devops-dv";
            Program _program = new Program();

            foreach (var sub in azure.GetSubscriptions())
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                SubscriptionData? subscription = sub.Data;
                if (subscription.TenantId.ToString() != _targetTenant)
                {
                    Console.WriteLine($"Skipping {subscription.DisplayName} - not target tenant.");
                    continue;
                }
                if (!String.IsNullOrEmpty(_targetSubscription) && subscription.SubscriptionId != _targetSubscription)
                {
                    Console.WriteLine($"Skipping {subscription.DisplayName} - not target subscription.");
                    continue;
                }

                Console.WriteLine($"Start updating {subscription.DisplayName} ({subscription.SubscriptionId})...");
                await UpdateSubscriptionTags(sub);
                await UpdateResourceGroupsTags(sub);

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;

                Console.WriteLine($"Finished updating {subscription.DisplayName} ({subscription.SubscriptionId}) in {ts.TotalSeconds} seconds.");
            }



            async Task UpdateSubscriptionTags(SubscriptionResource subscription)
            {
                Dictionary<string, string> subscriptionTags = new Dictionary<string, string>(subscription.Data.Tags);

                if (subscriptionTags?.Count > 0)
                {
                    var updatedTags = _program.UpdateTags(subscriptionTags, subscription.Data.DisplayName);

                    // Construct the TagResourceData object required to pass to subscription tag update
                    Tag tag = new();
                    foreach (var updatedTag in updatedTags)
                    {
                        tag.TagValues.Add(updatedTag.Key, updatedTag.Value);
                    };
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
                foreach (ResourceGroupResource resourceGroup in subscription.GetResourceGroups())
                {
                    string resourceGroupName = resourceGroup.Data.Name;
                    if (!String.IsNullOrEmpty(_targetResourceGroup) && resourceGroupName != _targetResourceGroup) continue;

                    var resourceGroupTags = resourceGroup?.Data?.Tags;

                    if (resourceGroupTags?.Count > 0)
                    {
                        var updatedTags = _program.UpdateTags(resourceGroupTags, resourceGroupName);
                        await resourceGroup.SetTagsAsync(updatedTags);
                    }
                    else
                    {
                        Console.WriteLine($"No tags on resource {resourceGroupName}");
                    }
                    await UpdateResourcesTags(subscription, resourceGroup);
                }
            }

            async Task UpdateResourcesTags(SubscriptionResource subscription, ResourceGroupResource resourceGroup)
            {
                var resources = resourceGroup.GetGenericResources();

                if (resources != null)
                {
                    foreach (var resource in resources)
                    {
                        var resourceTags = resource?.Data?.Tags;
                        var resourceName = resource?.Data?.Name;

                        if (resourceTags?.Count > 0)
                        {
                            var updatedTags = _program.UpdateTags(resourceTags, resourceName);
                            await resource.SetTagsAsync(updatedTags);
                        }
                        else
                        {
                            Console.WriteLine($"No tags on resource {resourceName}");
                        }
                    }
                }
            }
        }

        public Dictionary<string, string> UpdateTags(IDictionary<string, string> currentTags, string itemName)
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
                    Console.WriteLine($"Changing tag {tag.Key} to {_tagKeysNeedingUpdated[tag.Key]} for item {itemName}");
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