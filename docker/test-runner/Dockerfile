FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /workspace

# Install curl for health checks and other small utilities
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# By default copy the repository into the image. When running via compose we
# mount the repository as a volume which will override this copy, but copying
# here allows building the image and verifying Dockerfile syntax.
COPY . /workspace

# Default entrypoint: run the single test specified by the compose command.
ENTRYPOINT ["bash","-c"]
