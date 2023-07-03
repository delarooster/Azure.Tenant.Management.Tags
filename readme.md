# Azure Tag Update Utility

## Overview

The Azure Tag Update Utility is a streamlined tool developed for managing Azure resources effectively. This utility automates the task of updating tags across Azure subscriptions, resource groups, and resources within them. The tool can be customized to suit different environments and requirements, making it a powerful instrument for easy large-scale resource management in Azure.

## Features

- Specify target Azure tenants, subscriptions, and resource groups for tag updates.
- Automated batch update of tags across subscriptions, resource groups, and resources.
- Define a mapping of current tag names to new tag names in a YAML file for flexible and persistent configurations.
- Performance tracking via stopwatch functionality, with detailed console logging for transparency and troubleshooting.

## How it works

This utility uses Azure's ARM client to gather all subscriptions associated with a user's authentication. It then iterates over each subscription, updating tags on the subscription itself, and subsequently on its resource groups and resources within those groups.

Specific tags targeted for update are defined in a YAML file that is read during the initialization of the application. For each target tag key, the utility updates the key while retaining the original value. The usage of a YAML file provides flexibility and the ease of updates without requiring code changes.

For example, if you want to rename the tag key "Client" to "Customer" across all selected Azure resources while maintaining the same values, this utility will handle that task efficiently.

## Usage

1. Configure the YAML file (e.g., `tags.yaml`) containing the tags you want to update. The key in the YAML is the current tag, and the value is the new tag name.
2. Set up `targetTenant`, `targetSubscription`, and `targetResourceGroup` in the main method. These are used to specify the scope of the tag updates.
3. Run the application. It will iterate over all subscriptions, resource groups, and resources as per the scope specified, updating the tags as per the mapping provided in the YAML file.

The console will display real-time updates about the subscription, resource group, and resource tags being updated. For each subscription, the utility logs the time taken to complete the updates, providing valuable insight into performance.

### Test Suite
The Azure Tag Update Utility includes a robust test suite that validates and ensures the correctness of the tag updating logic. Utilizing the xUnit framework, this test suite provides coverage for various scenarios, ensuring the utility behaves as expected under different conditions.

These tests are structured according to the Arrange-Act-Assert pattern and are designed to ensure the code's reliability and maintainability. The test suite verifies that the utility performs as intended and safeguards against future modifications breaking the current functionality. 

## Important Notes

- Please ensure that the Azure user has sufficient privileges to update the tags on the resources.
- This utility is designed for simplicity and ease of use, and it provides an effective solution for basic Azure tag management. However, for complex tag management scenarios, more advanced solutions may be required.
- This tool assumes that the subscriptions, resource groups, and resources requiring updates are independent of each other, and does not account for dependencies or conflicts that may arise due to tag updates.
- As this utility makes several requests to the Azure API, please be mindful of potential rate limiting issues. It is recommended to test the tool with a smaller scope before deploying it at scale.
- Make sure the `tags.yaml` file exists and is in the correct format. An incorrect or missing file will result in a runtime error.

## Repository Visualizer
![Visualization of the codebase](./images/diagram.svg)

---

This utility represents a simple, yet powerful, tool for managing tags across Azure resources. It helps maintain consistency and uniformity, leading to better organization and tracking of resources. We hope this utility proves useful in your Azure resource management efforts.