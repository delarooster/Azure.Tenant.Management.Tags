using System.Diagnostics;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;

internal class Program
{
    /// <summary>
    /// Small application to update the tags in Azure on subscriptions, resource groups, and resources.
    /// Currently reads from a hard-coded Dictionary of targeted keys; the value is the intended transformation
    /// e.g. Looking for "Client" tag key and updating to "Customer" tag key, retaining same value as prior
    /// Starts by collecting all subscriptions a user is authenticated, then subscription by subscription
    /// iterates over the list, updating tags on subscription, then its resource groups, and the resources
    /// within the group.
    /// </summary>
    /// <returns></returns>
    private static async Task Main()
    {
        ArmClient azure = new(new DefaultAzureCredential());
        const string targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
        const string targetSubscription = "cb70135b-a87f-47c4-adc2-9e172bc22f88";
        const string targetResourceGroup = "";

        var tagKeysNeedingUpdated = new Dictionary<string, string>()
        {
            { "Client", "Customer" },
            { "Application", "Project" },
            { "App", "Project" }
        };


        foreach (var sub in azure.GetSubscriptions())
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            SubscriptionData? subscription = sub.Data;
            if (subscription.TenantId.ToString() != targetTenant)
            {
                Console.WriteLine($"Skipping {subscription.DisplayName} - not target tenant.");
                continue;
            }
            if (!String.IsNullOrEmpty(targetSubscription) && subscription.SubscriptionId != targetSubscription)
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
                var updatedTags = UpdateTags(subscriptionTags, subscription.Data.DisplayName);
                
                // Construct the TagResourceData object required to pass to subscription tag update
                Tag newTag = new();
                foreach (var tag in updatedTags)
                {
                    newTag.TagValues.Add(tag.Key, tag.Value);
                };
                TagResourceData newTags = new(newTag);
                const Azure.WaitUntil wait = new();
                await subscription.GetTagResource().CreateOrUpdateAsync(wait, newTags);
            }
            else
            {
                Console.WriteLine($"No tags on resource {subscription.Data.DisplayName}");
            }
        }

        async Task UpdateResourceGroupsTags(SubscriptionResource subscription)
        {
            await foreach (ResourceGroupResource resourceGroup in subscription.GetResourceGroups())
            {
                string resourceGroupName = resourceGroup.Data.Name;
                if (!String.IsNullOrEmpty(targetResourceGroup) && resourceGroupName != targetResourceGroup) continue;

                var resourceGroupTags = resourceGroup?.Data?.Tags;

                if (resourceGroupTags?.Count > 0)
                {
                    var updatedTags = UpdateTags(resourceGroupTags, resourceGroupName);
                    await resourceGroup.SetTagsAsync(updatedTags);
                    await UpdateResourcesTags(subscription, resourceGroup);
                }
                else
                {
                    Console.WriteLine($"No tags on resource {resourceGroupName}");
                }
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
                        var updatedTags = UpdateTags(resourceTags, resourceName);
                        await resource.SetTagsAsync(updatedTags);
                    }
                    else
                    {
                        Console.WriteLine($"No tags on resource {resourceName}");
                    }
                }
            }
        }

        Dictionary<string, string> UpdateTags(IDictionary<string, string> currentTags, string itemName)
        {
            var originalTags = new Dictionary<string, string>(currentTags);
            Dictionary<string, string> updatedTags = new();

            var markedTagsForUpdate = currentTags
                    .Where(tag => tagKeysNeedingUpdated.ContainsKey(tag.Key))
                    .ToList();

            if (markedTagsForUpdate?.Count > 0)
            {
                updatedTags = markedTagsForUpdate.ToDictionary(x => x.Key, x => x.Value);
                markedTagsForUpdate.ForEach(tag =>
                {
                    Console.WriteLine($"Changing tag {tag.Key} to {tagKeysNeedingUpdated[tag.Key]} for item {itemName}");
                    // Add new tag key, retaining existing value
                    updatedTags[tagKeysNeedingUpdated[tag.Key]] = tag.Value;
                    // Remove old tag from both dictionaries
                    updatedTags.Remove(tag.Key);
                    originalTags.Remove(tag.Key);
                });

                if (updatedTags.Count > 0)
                {
                    // Recombine existing tags with updated tags
                    foreach (var tag in originalTags)
                    {
                        if (!updatedTags.ContainsKey(tag.Key))
                        {
                            updatedTags.Add(tag.Key, tag.Value);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"No updated tags to recombine for item {itemName}. Check if the tags were properly updated.");
                }
            }
            else
            {
                Console.WriteLine($"No tags marked for update in item {itemName}. Ensure the tags to be updated are properly identified.");
            }



            return updatedTags;
        }
    }
}