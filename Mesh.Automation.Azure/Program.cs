using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;

internal class Program
{
    private static async Task Main(string[] args)
    {
        ArmClient azure = new ArmClient(new DefaultAzureCredential());
        var httpClient = new HttpClient();
        Uri baseUri = new Uri("https://management.azure.com/");

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
        // make sure it's the MESH tenant
        // Update tags
        // Then the resource groups in each sub
        // Then the resources in each rg

        // TODO remove
        //var subscriptionsJson = await httpClient.GetStringAsync(new Uri(baseUri, "subscriptions?api-version=2014-04-01"));
        //List<Subscription>? subscriptions = JsonSerializer.Deserialize<Root>(subscriptionsJson).Value;
        var azureSubscriptions = azure.GetSubscriptions();

        foreach (var sub in azureSubscriptions)
        {
            SubscriptionData? subscription = sub.Data;
            if (subscription.SubscriptionId != "cb70135b-a87f-47c4-adc2-9e172bc22f88") continue;
            //var subscriptionResource = SubscriptionResource.CreateResourceIdentifier(subscription.SubscriptionId);
            //SubscriptionResource subscription = azure.GetSubscriptionResource(subscriptionResource);
            Console.WriteLine($"Updating: {subscription.DisplayName}");
            Console.WriteLine($"Id: {subscription.SubscriptionId}");
            await UpdateSubscriptionTags(sub);
            UpdateResourceGroupsTags(sub);
        }

        async Task UpdateSubscriptionTags(SubscriptionResource subscription)
        {
            // TODO remove later
            //string? result = await httpClient.GetStringAsync(new Uri(baseUri, $"subscriptions/{subscriptionId}/tagNames?api-version=2017-05-10"));
            //List<SubscriptionTag>? subscriptionTags = JsonSerializer.Deserialize<TagRoot>(result).Value;

            var subscriptionTags = subscription.Data.Tags;
            var tagResource = subscription.GetTagResource();

            var tagsToChange = subscriptionTags?
                    .Where(tag => tagsToCheck.ContainsKey(tag.Key))
                    .ToList();

            if (tagsToChange != null && tagsToChange.Count > 0)
            {
                var tagsToUpdate = tagsToChange.ToDictionary(x => x.Key, x => x.Value);
                foreach (var tag in tagsToChange)
                {
                    Console.WriteLine($"Changing tag {tag.Key} to {tagsToCheck[tag.Key]} for {subscription.Data.DisplayName}");
                    var oldTagValue = tag.Value;

                    // Add new tag to subscription
                    tagsToUpdate[tagsToCheck[tag.Key]] = oldTagValue;

                    // Remove old tag
                    tagsToUpdate.Remove(tag.Key);
                }
                var wait = new Azure.WaitUntil();

                Tag newTag = new Tag();
                foreach (var tag in tagsToUpdate)
                {
                    newTag.TagValues.Add(tag.Key, tag.Value);
                }
                TagResourceData newTags = new TagResourceData(newTag);
                tagResource.CreateOrUpdate(wait, newTags);
                
            }


            //List<string> tagsToRemove = new List<string>(); // This will store the tags to be removed

            //if (tagResource != null)
            //{
            //    // Assuming tagResource is similar to a dictionary where the keys are tag names and the values are tag values
            //    foreach (var tag in tagResource.Data.TagValues)
            //    {
            //        // Check if the tag name is in the list of tags to check
            //        if (tag.Key != null && tagsToCheck.ContainsKey(tag.Key))
            //        {
            //            Console.WriteLine($"Changing tag {tag.Key} for {subscription.Data.DisplayName}");

            //            var newKey = tagsToCheck[tag.Key];
            //            Console.WriteLine($"Updating tag key {tag.Key} to {newKey} with value {tag.Value}");

            //            tagResource.
            //            await subscription.CreateOrUpdatePredefinedTagValueAsync(newKey, tag.Value);

            //            // Add the old tag to the list of tags to be removed
            //            tagsToRemove.Add(tag.Key);
            //        }
            //    }

            //    // Now you have a list of tags that were updated, and you can use this list to remove them later
            //    foreach (var tagToRemove in tagsToRemove)
            //    {
            //        Console.WriteLine($"Removing tag key {tagToRemove}");
            //        await subscription.DeletePredefinedTagAsync(tagToRemove);
            //    }
            //}

        }

        async Task UpdateResourceGroupsTags(SubscriptionResource subscription)
        {
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();

            await foreach (ResourceGroupResource group in resourceGroups)
            {
                string resourceGroupName = group.Data.Name;
                // TODO update to use default tags object
                var tags = group.GetTagResource();
                //var tags = group.Data.Tags;
                string? result = await httpClient.GetStringAsync(new Uri(baseUri, $"subscriptions/{subscription.Id}/resourceGroups/{resourceGroupName}?api-version=2014-04-01"));
                ResourceGroup? resourceGroup = JsonSerializer.Deserialize<ResourceGroup>(result);


                var tagsToChange = resourceGroup?.Tags?
                    .Where(tag => tagsToCheck.ContainsKey(tag.Key))
                    .ToList();

                if (tagsToChange != null && tagsToChange.Count > 0)
                {
                    var tagsToUpdate = tagsToChange.ToDictionary(x => x.Key, x => x.Value);
                    foreach (var tag in tagsToChange)
                    {
                        Console.WriteLine($"Changing tag {tag.Key} to {tagsToCheck[tag.Key]} for {resourceGroupName}");
                        var oldTagValue = tag.Value;

                        // Add new tag to resourceGroup
                        tagsToUpdate[tagsToCheck[tag.Key]] = oldTagValue;

                        // Remove old tag
                        tagsToUpdate.Remove(tag.Key);
                    }

                    group.SetTags(tagsToUpdate);
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
}

// Subscription Tags
public class Count
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }
}

public class Value
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("tagValue")]
    public string? TagValue { get; set; }
    [JsonPropertyName("count")]
    public Count? Count { get; set; }
}

public class SubscriptionTag
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("tagName")]
    public string? TagName { get; set; }
    [JsonPropertyName("count")]
    public Count? Count { get; set; }
    [JsonPropertyName("values")]
    public List<Value>? Values { get; set; }
}

public class TagRoot
{
    [JsonPropertyName("value")]
    public List<SubscriptionTag>? Value { get; set; }
}

// Subscriptions
public class SubscriptionPolicies
{
    [JsonPropertyName("locationPlacementId")]
    public string? LocationPlacementId { get; set; }
    [JsonPropertyName("quotaId")]
    public string? QuotaId { get; set; }
    [JsonPropertyName("spendingLimit")]
    public string? SpendingLimit { get; set; }
}

public class Subscription
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("state")]
    public string? State { get; set; }
    [JsonPropertyName("subscriptionPolicies")]
    public SubscriptionPolicies? SubscriptionPolicies { get; set; }
}

public class Root
{
    [JsonPropertyName("value")]
    public List<Subscription>? Value { get; set; }
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