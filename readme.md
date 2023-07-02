# Azure Tag Updater

This C# program is designed to update the tags in Azure subscriptions, resource groups, and resources. It currently reads from a hardcoded dictionary of targeted keys and applies the corresponding transformations. For example, it can change a "Client" tag key to a "Customer" tag key, retaining the same value as before.

The program starts by collecting all subscriptions that the authenticated user has access to. Then, it iterates over each subscription, updating tags on the subscription itself, its resource groups, and the resources within each group.

The `Main` method is the entry point of the program, initializing an Azure Resource Manager (ARM) client with the default Azure credentials. The credentials are used to authenticate the user, and the ARM client is used to interact with Azure resources.

Here's a brief walkthrough of the code:

## Accessing Subscriptions

The program retrieves all the subscriptions that the authenticated user has access to by calling `azure.GetSubscriptions()`. It then loops through each subscription. If the subscription matches a target tenant or subscription defined in the code, the program updates its tags and the tags of its resource groups.

```csharp
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
```

## Updating Subscription Tags

The `UpdateSubscriptionTags` method is responsible for updating the tags of a given subscription. It retrieves the current tags of the subscription, determines which tags need to be updated, and applies the updates. The updated tags, along with the original tags that weren't updated, are then passed to the subscription's `CreateOrUpdateAsync` method to update the subscription's tags in Azure.

For more information, you can refer to the [Azure Resource Manager SDK](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/resources?view=azure-dotnet-3.1).

## Updating Resource Groups Tags

The `UpdateResourceGroupsTags` method updates the tags of a given subscription's resource groups. It retrieves the resource groups of the subscription, determines which tags need to be updated, and applies the updates. The updated tags, along with the original tags that weren't updated, are then passed to the resource group's `SetTagsAsync` method to update the resource group's tags in Azure.

## Updating Resource Tags

The `UpdateResourceTags` method updates the tags of the resources in a given resource group. It retrieves the resources of the resource group, determines which tags need to be updated, and applies the updates. The updated tags, along with the original tags that weren't updated, are then passed to the resource's `SetTagsAsync` method to update the resource's tags in Azure.

## Getting Access Token

The `GetAccessTokenAsync` method is used to retrieve an access token for authentication purposes. The token is necessary for authorizing HTTP requests made to Azure.

The program also defines classes (`ResourceGroup` and `Properties`) to represent resource groups and their properties for JSON deserialization.

For more information about working with Azure resources, you can refer to [Azure Management Libraries for .NET](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/management?view=azure-dotnet). For more about Azure authentication, you can refer to [Authenticate with the Azure libraries](https://docs.microsoft.com/en-us/dotnet/azure/authentication?view=azure-dotnet).