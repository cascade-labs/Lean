# Makefile for Cascade Labs LEAN Container
#
# Usage:
#   make fast           - FAST build: DataSource projects only on pre-built LEAN (recommended)
#   make lean_container - SLOW build: Rebuild entire LEAN from source (lean-cli method)
#   make clean          - Clean build artifacts
#   make info           - Show current configuration
#
# Prerequisites:
#   - Docker available
#   - (optional) lean-cli for the slow full rebuild

.PHONY: fast lean_container setup compile clean check-deps stubs stubs_install info all

# Image tags
IMAGE_TAG ?= cascadelabs-lean
ENGINE_IMAGE := lean-cli/engine:$(IMAGE_TAG)
RESEARCH_IMAGE := lean-cli/research:$(IMAGE_TAG)

# All DataSource projects
DATASOURCES := CascadeHyperliquid CascadeThetaData CascadeKalshiData CascadeTradeAlert

LAUNCHER_CSPROJ := Launcher/QuantConnect.Lean.Launcher.csproj

#############################################################################
# FAST BUILD: Compile DataSource projects only, layer on pre-built LEAN
# This is 10-100x faster than rebuilding everything from source
#############################################################################
fast: check-docker
	@echo "=== Fast Build: DataSource Projects Only ==="
	@echo "Base image: quantconnect/lean:latest (pre-built LEAN)"
	@echo "Output tag: $(ENGINE_IMAGE)"
	@echo ""
	docker build -f Dockerfile.datasources -t $(ENGINE_IMAGE) .
	@echo ""
	@echo "=== Build Complete ==="
	@echo ""
	@echo "To use the custom image:"
	@echo "  lean config set engine-image $(ENGINE_IMAGE)"
	@echo ""

#############################################################################
# FULL BUILD: Compile locally and build container
# Uses local compilation (faster than in-container) with official foundation
#############################################################################
lean_container: check-deps setup compile
	@echo "=== Building Cascade Labs Custom LEAN Container ==="
	@echo "Image tag: $(IMAGE_TAG)"
	@echo ""
	@echo "NOTE: Consider 'make fast' for DataSource-only changes (much faster)"
	@echo ""
	@# Build engine image directly using official foundation (skip foundation rebuild)
	@# Uses docker wrapper script to route to podman
	cd .. && PATH="$(CURDIR)/scripts:$$PATH" docker build -t lean-cli/engine:$(IMAGE_TAG) -f Lean/Dockerfile .
	@echo ""
	@echo "=== Build Complete ==="
	@echo ""
	@echo "Setting as default engine image..."
	@lean config set engine-image lean-cli/engine:$(IMAGE_TAG)
	@echo ""
	@echo "Custom image ready: lean-cli/engine:$(IMAGE_TAG)"

# Compile LEAN locally (much faster than inside container)
compile:
	@echo "=== Compiling LEAN ==="
	dotnet build QuantConnect.Lean.sln -c Debug --nologo -v q
	@echo "Compilation complete"
	@echo ""

#############################################################################
# LEAN-CLI BUILD: Full rebuild from source using lean-cli
# Alternative method that uses lean build command
#############################################################################
lean_container_leancli: check-deps setup
	@echo "=== Full LEAN Build (slow, compiles everything from source) ==="
	@echo "Image tag: $(IMAGE_TAG)"
	@echo ""
	@echo "NOTE: Consider 'make fast' for DataSource-only changes (much faster)"
	@echo ""
	@# Add scripts directory to PATH for docker wrapper (uses podman)
	@# lean build expects to be run from parent of Lean/ directory
	PATH="$(CURDIR)/scripts:$$PATH" && cd .. && lean build --tag $(IMAGE_TAG) .
	@echo ""
	@echo "=== Build Complete ==="
	@echo ""
	@echo "To use the custom image:"
	@echo "  lean config set engine-image $(ENGINE_IMAGE)"
	@echo "  lean config set research-image $(RESEARCH_IMAGE)"

# Setup: Add DataSource project references to Launcher (for full source builds)
setup:
	@echo "=== Setting up DataSource Project References ==="
	@for ds in $(DATASOURCES); do \
		if [ -d "DataSource/$$ds" ]; then \
			if grep -q "$$ds" $(LAUNCHER_CSPROJ) 2>/dev/null; then \
				echo "  $$ds: already referenced"; \
			else \
				echo "  $$ds: adding reference..."; \
				sed -i.bak 's|</Project>|  <ItemGroup>\n    <ProjectReference Include="../DataSource/'$$ds'/'$$ds'.csproj" />\n  </ItemGroup>\n</Project>|' $(LAUNCHER_CSPROJ); \
				rm -f $(LAUNCHER_CSPROJ).bak; \
			fi \
		else \
			echo "  $$ds: not found in DataSource/, skipping"; \
		fi \
	done
	@echo ""

# Check Docker is available
check-docker:
	@command -v docker >/dev/null 2>&1 || { echo "Error: docker not found"; exit 1; }
	@echo "Docker: OK"

# Check all dependencies (for full build)
check-deps: check-docker
	@echo "=== Checking Dependencies ==="
	@command -v lean >/dev/null 2>&1 || { echo "Error: lean-cli not found. Install with: pip install lean"; exit 1; }
	@echo "  lean-cli: OK"
	@echo "  container runtime: OK"
	@echo ""

# Clean build artifacts
clean:
	@echo "=== Cleaning Build Artifacts ==="
	find . -type d -name "bin" -exec rm -rf {} + 2>/dev/null || true
	find . -type d -name "obj" -exec rm -rf {} + 2>/dev/null || true
	@echo "Done"

# Show current configuration
info:
	@echo "=== Cascade Labs LEAN Configuration ==="
	@echo ""
	@echo "DataSource projects:"
	@for ds in $(DATASOURCES); do \
		if [ -d "DataSource/$$ds" ]; then \
			echo "  - $$ds"; \
		fi \
	done
	@echo ""
	@echo "Build targets:"
	@echo "  make fast           - Quick build (DataSource only, recommended)"
	@echo "  make lean_container - Full rebuild from source (slow)"
	@echo ""
	@echo "Current engine image:"
	@lean config get engine-image 2>/dev/null || echo "  (not set)"
	@echo ""

# Generate Python stubs from LEAN source
stubs:
	@echo "=== Generating LEAN Python Stubs ==="
	@./scripts/stubs.sh

# Install stubs in editable mode
stubs_install: stubs
	@echo "=== Installing LEAN Python Stubs ==="
	python3 -m pip install --break-system-packages -e .stubs/output
	@echo "Done! LEAN stubs installed."

# Build container AND install stubs
all: lean_container stubs_install
	@echo ""
	@echo "=== All Complete ==="
