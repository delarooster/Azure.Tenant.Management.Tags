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

        [Fact]
        public void NoTagsTest()
        {
            // Arrange
            var originalTags = new Dictionary<string, string> { };

            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert
            var expectedTags = new Dictionary<string, string> { };

            Assert.Equal(expectedTags, updatedTags);
        }

        [Fact]
        public void SingleTagValueUpdate()
        {
            // Arrange - only one tag that needs value update
            var originalTags = new Dictionary<string, string>
        {
            {"Customer", "Internal"}
        };
            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert
            var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Mesh"}
        };

            Assert.Equal(expectedTags, updatedTags);
        }

        [Fact]
        public void CaseSensitiveValueTest()
        {
            // Tag values are case-sensitive, so "internal" should not match "Internal"
            // Arrange
            var originalTags = new Dictionary<string, string>
        {
            {"Customer", "internal"}, // lowercase, should not be updated
            {"Project", "Internal"}   // uppercase, should be updated to "Mesh"
        };
            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert
            var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "internal"}, // unchanged (case-sensitive)
            {"Project", "Mesh"}       // updated from "Internal"
        };

            Assert.Equal(expectedTags, updatedTags);
        }

        [Fact]
        public void MultipleTagsWithSameValueToUpdate()
        {
            // Multiple tags with the same value that needs updating
            // Arrange
            var originalTags = new Dictionary<string, string>
        {
            {"Customer", "Internal"},
            {"Project", "Internal"},
            {"Environment", "Internal"},
            {"CostCenter", "Internal"}
        };
            var itemName = "storageAccount";

            var updatedTags = _program.UpdateTagValues(new AzureResource(originalTags, itemName));

            // Assert - all "Internal" values should be updated to "Mesh"
            var expectedTags = new Dictionary<string, string>
        {
            {"Customer", "Mesh"},
            {"Project", "Mesh"},
            {"Environment", "Mesh"},
            {"CostCenter", "Mesh"}
        };

            Assert.Equal(expectedTags, updatedTags);
        }
    }
}

