
using Azure.Tenant.Automation;

namespace Azure.Tenant.Automation.Tests;

public class TagUpdateTests
{
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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

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

        Program program = new Program();
        var updatedTags = program.UpdateTags(originalTags, itemName);

        // Assert
        var expectedTags = new Dictionary<string, string>
        {
            {"foo", "sub"}
        };

        Assert.Equal(expectedTags, updatedTags);
    }
}