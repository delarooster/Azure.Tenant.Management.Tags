
using Azure.Tenant.Automation;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Azure.Tenant.Automation.Tests;

public class TagKeysUpdateTests
{
    Program _program;

    public TagKeysUpdateTests()
    {
        _program = new();
    }

    [Fact]
    public void HappyPath()
    {
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"Client", "Contoso"},
            {"Application", "Olympus"},
            {"Environment", "Dv"},
            {"foo", "sub"}
        };
        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Contoso"},
            {"Project", "Olympus"},
            {"Environment", "Dv"},
            {"foo", "sub"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void ClientTagTest()
    {
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"Client", "Contoso"}
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Contoso"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void ClientAndFooTagTest()
    {
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"Client", "Contoso"},
            {"foo", "resource"}
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Contoso"},
            {"foo", "resource"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void ApplicationTagTest()
    {
        // If there are tags of both App and Application
        // Only one tag of Project name is returned
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"App", "Ganymede"},
            {"Application", "Ganymede"}
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"Project", "Ganymede"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void NoChangeTagTest()
    {
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"foo", "sub"},
            {"Environment", "Dv"},
            {"CostCenter", "Internal"}
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"foo", "sub"},
            {"Environment", "Dv"},
            {"CostCenter", "Internal"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void NoTagsTest()
    {
        // Arrange
        var originalTags = new Dictionary<string, string> { };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string> { };

        Assert.Equal(expectedTags, updatedTags);
    }
    [Fact]
    public void NoChangeOneTagTest()
    {
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"foo", "sub"}
        };

        var itemName = "Dv-AD-Sdbx";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"foo", "sub"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }

    [Fact]
    public void NewKeyAlreadyExistsWithDifferentValue()
    {
        // If the new key already exists, the update should be skipped
        // and the old key should still be removed (potential data loss scenario)
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"Client", "Contoso"},
            {"Customer", "ExistingValue"} // New key already exists with different value
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        // The "Client" key should be removed, but "Customer" should keep its original value
        var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "ExistingValue"} // Original value preserved, "Client" removed
        };

        Assert.Equal(expectedTags, updatedTags);
    }

    [Fact]
    public void NewKeyAlreadyExistsWithSameValue()
    {
        // If the new key already exists with the same value, the update should be skipped
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"Client", "Contoso"},
            {"Customer", "Contoso"} // New key already exists with same value
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        // The "Client" key should be removed, "Customer" keeps its value
        var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Contoso"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }

    [Fact]
    public void CaseSensitiveKeyTest()
    {
        // Tag keys are case-sensitive, so "client" should not match "Client"
        // Arrange
        var originalTags = new Dictionary<string, string>
        {
            {"client", "Contoso"}, // lowercase, should not be updated
            {"Client", "Contoso"}  // uppercase, should be updated to "Customer"
        };

        var itemName = "storageAccount";

        var updatedTags = _program.UpdateTagKeys(new AzureResource(originalTags, itemName));

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"client", "Contoso"}, // unchanged (case-sensitive)
            {"Customer", "Contoso"} // updated from "Client"
        };

        Assert.Equal(expectedTags, updatedTags);
    }
}