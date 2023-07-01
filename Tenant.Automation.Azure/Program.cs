using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;

internal class Program
{
    private static async Task Main()
    {
        ArmClient azure = new(new DefaultAzureCredential());
        var httpClient = new HttpClient();
        Uri baseUri = new("https://management.azure.com/");
        string targetTenant = "d49110b2-6f26-4c66-b723-1729cdb9a3cf";
        string targetSubscription = "cb70135b-a87f-47c4-adc2-9e172bc22f88";

        var tagsToCheck = new Dictionary<string, string>()
        {
            { "Client", "Customer" },
            { "Application", "Project" },
            { "App", "Project" }
        };

        var token = await GetAccessTokenAsync(new DefaultAzureCredential(), "https://management.azure.com/");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        //SubscriptionCollection subscriptions = azure.GetSubscriptions();
        // get all the subscriptions
        // make sure it's the correct tenant
        // Update tags
        // Then the resource groups in each sub
        // Then the resources in each rg

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
                });

                // ! For subscriptions only these tags will be left once the update runs
                if(tagsToUpdate.Count > 0) {
                    Tag newTag = new();
                    tagsToUpdate.ForEach(tag =>
                    {
                        newTag.TagValues.Add(tag.Key, tag.Value);
                    });
                    TagResourceData newTags = new(newTag);
                    const Azure.WaitUntil wait = new();
                    await tagResource.CreateOrUpdateAsync(wait, newTags);
                }
            }
        }

        async Task UpdateResourceGroupsTags(SubscriptionResource subscription)
        {

            // TODO update to use default tags object
            //var tags = group.GetTagResource();
            await foreach (ResourceGroupResource group in subscription.GetResourceGroups())
            {
                string resourceGroupName = group.Data.Name;
                var tags = group.Data.Tags;
                string? result = await httpClient.GetStringAsync(new Uri(baseUri, $"subscriptions/{subscription.Data.SubscriptionId}/resourceGroups/{resourceGroupName}?api-version=2014-04-01"));
                ResourceGroup? resourceGroup = JsonSerializer.Deserialize<ResourceGroup>(result);

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
                    });
                    await group.SetTagsAsync(tagsToUpdate);
                }
                await UpdateResourceTags(subscription, group);
            }
        }

        async Task UpdateResourceTags(SubscriptionResource subscription, ResourceGroupResource resourceGroup)
        {
            var resource = resourceGroup.GetGenericResourcesAsync();
            // TODO wip
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