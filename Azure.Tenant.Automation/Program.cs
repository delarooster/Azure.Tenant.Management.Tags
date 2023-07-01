using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using static Program;

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
        var httpClient = new HttpClient();
        Uri baseUri = new("https://management.azure.com/");
        const string targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
        const string targetSubscription = "cb70135b-a87f-47c4-adc2-9e172bc22f88";
        const string targetResourceGroup = "rg-devops-dv";

        var tagsToCheck = new Dictionary<string, string>()
        {
            { "Client", "Customer" },
            { "Application", "Project" },
            { "App", "Project" }
        };

        var token = await GetAccessTokenAsync(new DefaultAzureCredential(), "https://management.azure.com/");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        foreach (var sub in azure.GetSubscriptions())
        {
            SubscriptionData? subscription = sub.Data;
            if (subscription.TenantId.ToString() != targetTenant) continue;
            if (!String.IsNullOrEmpty(targetSubscription) && subscription.SubscriptionId != targetSubscription) continue;
            Console.WriteLine($"Updating: {subscription.DisplayName}");
            Console.WriteLine($"Id: {subscription.SubscriptionId}");
            await UpdateSubscriptionTags(sub);
            await UpdateResourceGroupsTags(sub);
        }

        async Task UpdateSubscriptionTags(SubscriptionResource subscription)
        {
            var subscriptionTags = subscription.Data.Tags;
            var tagResource = subscription.GetTagResource();
            var tagsToChange = subscriptionTags?
                    .Where(tag => tagsToCheck.ContainsKey(tag.Key))
                    .ToList();

            if (subscriptionTags != null)
            {
                // Save the original tags
                var originalTags = new Dictionary<string, string>(subscriptionTags);

                if (tagsToChange?.Count > 0)
                {
                    var tagsToUpdate = tagsToChange.ToDictionary(x => x.Key, x => x.Value);
                    tagsToChange.ForEach(tag =>
                    {
                        Console.WriteLine($"Changing tag {tag.Key} to {tagsToCheck[tag.Key]} for {subscription.Data.DisplayName}");
                        // Add new tag key to subscription, retaining old value
                        tagsToUpdate[tagsToCheck[tag.Key]] = tag.Value;
                        // Remove old tag
                        tagsToUpdate.Remove(tag.Key);
                        originalTags.Remove(tag.Key);
                    });

                    if (tagsToUpdate.Count > 0)
                    {
                        // Add the original tags that aren't being updated
                        foreach (var tag in originalTags)
                        {
                            if (!tagsToUpdate.ContainsKey(tag.Key))
                            {
                                tagsToUpdate.Add(tag.Key, tag.Value);
                            }
                        }
                        // Construct the TagResourceData object required to pass to subscription tag update
                        Tag newTag = new();
                        foreach (var tag in tagsToUpdate)
                        {
                            newTag.TagValues.Add(tag.Key, tag.Value);
                        };
                        TagResourceData newTags = new(newTag);
                        const Azure.WaitUntil wait = new();
                        await tagResource.CreateOrUpdateAsync(wait, newTags);
                    }
                }
            }
            else
            {
                Console.WriteLine($"No tags on resource {subscription.Data.DisplayName}");
            }
        }

        async Task UpdateResourceGroupsTags(SubscriptionResource subscription)
        {
            // TODO update to use default tags object
            //var tags = group.GetTagResource();
            await foreach (ResourceGroupResource group in subscription.GetResourceGroups())
            {
                string resourceGroupName = group.Data.Name;
                if (!String.IsNullOrEmpty(resourceGroupName) && resourceGroupName != targetResourceGroup) continue;
                var tags = group.Data.Tags;
                string? result = await httpClient.GetStringAsync(new Uri(baseUri, $"subscriptions/{subscription.Data.SubscriptionId}/resourceGroups/{resourceGroupName}?api-version=2014-04-01"));
                ResourceGroup? resourceGroup = JsonSerializer.Deserialize<ResourceGroup>(result);

                if (resourceGroup?.Tags != null)
                {
                    // Save the original tags
                    var originalTags = new Dictionary<string, string>(resourceGroup.Tags);

                    var tagsToChange = resourceGroup?.Tags?
                        .Where(tag => tagsToCheck.ContainsKey(tag.Key))
                        .ToList();

                    if (tagsToChange?.Count > 0)
                    {
                        var tagsToUpdate = tagsToChange.ToDictionary(x => x.Key, x => x.Value);
                        tagsToChange.ForEach(tag =>
                        {
                            Console.WriteLine($"Changing tag {tag.Key} to {tagsToCheck[tag.Key]} for {resourceGroupName}");
                            var oldTagValue = tag.Value;
                            // Add new tag to resourceGroup
                            tagsToUpdate[tagsToCheck[tag.Key]] = oldTagValue;
                            // Remove old tag
                            tagsToUpdate.Remove(tag.Key);
                            originalTags.Remove(tag.Key);
                        });

                        // Add the original tags that aren't being updated
                        foreach (var tag in originalTags)
                        {
                            if (!tagsToUpdate.ContainsKey(tag.Key))
                            {
                                tagsToUpdate.Add(tag.Key, tag.Value);
                            }
                        }

                        await group.SetTagsAsync(tagsToUpdate);
                    }
                    await UpdateResourceTags(subscription, group);
                }
                else
                {
                    Console.WriteLine($"No tags on resource {resourceGroup?.Name}");
                }
            }
        }

        async Task UpdateResourceTags(SubscriptionResource subscription, ResourceGroupResource resourceGroup)
        {
            var resources = resourceGroup.GetGenericResources();

            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    if (resource.Data?.Tags.Count > 0)
                    {
                        // Save the original tags
                        var originalTags = new Dictionary<string, string>(resource.Data.Tags);

                        var tagsToChange = originalTags
                            .Where(tag => tagsToCheck.ContainsKey(tag.Key))
                            .ToList();

                        if (tagsToChange?.Count > 0)
                        {
                            var tagsToUpdate = tagsToChange.ToDictionary(x => x.Key, x => x.Value);
                            tagsToChange.ForEach(tag =>
                            {
                                Console.WriteLine($"Changing tag {tag.Key} to {tagsToCheck[tag.Key]} for {resource.Data.Name}");
                                var oldTagValue = tag.Value;
                                // Add new tag to resource
                                tagsToUpdate[tagsToCheck[tag.Key]] = oldTagValue;
                                // Remove old tag
                                tagsToUpdate.Remove(tag.Key);
                                originalTags.Remove(tag.Key);
                            });

                            // Add the original tags that aren't being updated
                            foreach (var tag in originalTags)
                            {
                                if (!tagsToUpdate.ContainsKey(tag.Key))
                                {
                                    tagsToUpdate.Add(tag.Key, tag.Value);
                                }
                            }

                            await resource.SetTagsAsync(tagsToUpdate);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No tags on resource {resource.Data.Name}");
                    }
                }
            }
        }

        static async Task<string> GetAccessTokenAsync(TokenCredential credential, string scope)
        {
            var tokenRequestContext = new TokenRequestContext(new[] { scope });
            var tokenResponse = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
            return tokenResponse.Token;
        }
    }

    // Resource Groups
    public class ResourceGroup
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("location")]
        public string? Location { get; set; }
        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
        [JsonPropertyName("properties")]
        public Properties? Properties { get; set; }
    }

    public class Properties
    {
        [JsonPropertyName("provisioningState")]
        public string? ProvisioningState { get; set; }
    }
}