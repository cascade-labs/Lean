# Makefile for Cascade Labs LEAN Container
#
# Usage:
#   make lean_container - Build custom LEAN container with DataSource projects
#   make clean          - Clean build artifacts
#   make info           - Show current configuration
#
# Prerequisites:
#   - Docker available
#   - lean-cli installed (pip install lean)

.PHONY: lean_container setup compile clean check-deps stubs stubs_install info all

# Image tags
IMAGE_TAG ?= cascadelabs-lean
ENGINE_IMAGE := lean-cli/engine:$(IMAGE_TAG)
RESEARCH_IMAGE := lean-cli/research:$(IMAGE_TAG)

# All DataSource projects
DATASOURCES := CascadeHyperliquid CascadeThetaData CascadeKalshiData CascadeTradeAlert

LAUNCHER_CSPROJ := Launcher/QuantConnect.Lean.Launcher.csproj

#############################################################################
# Build custom LEAN container with DataSource projects
# Compiles locally (faster than in-container) then builds Docker image
#############################################################################
lean_container: check-deps setup compile
	@echo "=== Building Cascade Labs Custom LEAN Container ==="
	@echo "Image tag: $(IMAGE_TAG)"
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

# Setup: Add DataSource project references to Launcher
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

# Check all dependencies
check-deps:
	@echo "=== Checking Dependencies ==="
	@command -v docker >/dev/null 2>&1 || { echo "Error: docker not found"; exit 1; }
	@echo "  docker: OK"
	@command -v lean >/dev/null 2>&1 || { echo "Error: lean-cli not found. Install with: pip install lean"; exit 1; }
	@echo "  lean-cli: OK"
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
	@echo "Build target:"
	@echo "  make lean_container - Build custom LEAN container"
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
