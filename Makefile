.PHONY: build build-dev run run-dev stop clean shell logs help

# Variables
IMAGE_NAME := azure-tenant-automation
IMAGE_NAME_DEV := azure-tenant-automation-dev
CONTAINER_NAME := azure-tenant-automation
DOCKERFILE := Dockerfile
DOCKERFILE_DEV := Dockerfile.dev
SRC_DIR := src
AZURE_CONFIG_DIR := $(HOME)/.azure

# Default target
.DEFAULT_GOAL := help

# Build the Docker image
build:
	docker build -t $(IMAGE_NAME) -f $(DOCKERFILE) .

# Build the Docker image with Azure CLI (for local development)
build-dev:
	docker build -t $(IMAGE_NAME_DEV) -f $(DOCKERFILE_DEV) .

# Run the container
run:
	docker run --rm --name $(CONTAINER_NAME) $(IMAGE_NAME)

# Run the container with local config files and Azure CLI credentials mounted (for local development)
# Requires: build-dev and az login on host
run-dev: build-dev
	@if [ ! -d "$(AZURE_CONFIG_DIR)" ]; then \
		echo "Error: Azure CLI not configured. Please run 'az login' first."; \
		exit 1; \
	fi
	docker run --rm --name $(CONTAINER_NAME) \
		-v $(PWD)/$(SRC_DIR)/appsettings.json:/app/appsettings.json:ro \
		-v $(PWD)/$(SRC_DIR)/tags.yaml:/app/tags.yaml:ro \
		-v $(PWD)/$(SRC_DIR)/values.yaml:/app/values.yaml:ro \
		-v $(AZURE_CONFIG_DIR):/home/appuser/.azure \
		-e HOME=/home/appuser \
		-e AZURE_CONFIG_DIR=/home/appuser/.azure \
		$(IMAGE_NAME_DEV)

# Run the container in detached mode
run-detached:
	docker run -d --name $(CONTAINER_NAME) $(IMAGE_NAME)

# Stop the running container
stop:
	docker stop $(CONTAINER_NAME) || true
	docker rm $(CONTAINER_NAME) || true

# Get a shell in the container (requires running container)
shell:
	docker exec -it $(CONTAINER_NAME) /bin/sh

# View container logs
logs:
	docker logs -f $(CONTAINER_NAME)

# Clean up containers and images
clean: stop
	docker rmi $(IMAGE_NAME) || true
	docker rmi $(IMAGE_NAME_DEV) || true

# Clean everything including build cache
clean-all: clean
	docker builder prune -f

# Show help
help:
	@echo "Available targets:"
	@echo "  build          - Build the Docker image (production)"
	@echo "  build-dev      - Build the Docker image with Azure CLI (for local dev)"
	@echo "  run            - Run the container (removes on exit)"
	@echo "  run-dev        - Run with local config files and Azure CLI credentials (requires: build-dev, az login)"
	@echo "  run-detached   - Run the container in detached mode"
	@echo "  stop           - Stop and remove the running container"
	@echo "  shell          - Get a shell in the running container"
	@echo "  logs           - View container logs"
	@echo "  clean          - Remove container and image"
	@echo "  clean-all      - Clean everything including build cache"
	@echo "  help           - Show this help message"

