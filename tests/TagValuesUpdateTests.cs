using System;
namespace Azure.Tenant.Automation.Tests
{
	public class TagValuesUpdateTests
    {
        Program _program;
        public TagValuesUpdateTests()
		{
            _program = new();
        }
        [Fact]
        public void HappyPath()
        {
            // Arrange
            var originalTags = new Dictionary<string, string>
        {
            {"Customer", "Internal"},
            {"Project", "Internal"},
            {"Environment", "Dv"},
            {"foo", "sub"}
        };
            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert
            var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Mesh"},
            {"Project", "Mesh"},
            {"Environment", "Dv"},
            {"foo", "sub"}
        };

            Assert.Equal(expectedTags, updatedTags);
        }

        [Fact]
        public void NoChange()
        {
            // Arrange
            var originalTags = new Dictionary<string, string>
        {
            {"Customer", "Mesh"},
            {"Project", "Mesh"},
            {"Environment", "Sd"},
            {"foo", "sub"}
        };
            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert
            var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Mesh"},
            {"Project", "Mesh"},
            {"Environment", "Sd"},
            {"foo", "sub"}
        };

            Assert.Equal(expectedTags, updatedTags);
        }
    }
}

