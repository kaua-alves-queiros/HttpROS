# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY . .
# Explicitly publish the main project
RUN dotnet publish src/HttpROS.HttpROS/HttpROS.HttpROS.csproj -c Release -o /app

# Final stage - Use ASPNET image for YARP support
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app .

# Install dependencies: SSH server and sudo
RUN apt-get update && apt-get install -y \
    openssh-server \
    sudo \
    && rm -rf /var/lib/apt/lists/*

# Configure SSH
RUN mkdir /var/run/sshd
RUN sed -i 's/#Port 22/Port 50022/' /etc/ssh/sshd_config
RUN sed -i 's/#PasswordAuthentication yes/PasswordAuthentication yes/' /etc/ssh/sshd_config

# Create a user for SSH access
RUN useradd -m -s /bin/bash rosadmin && \
    echo "rosadmin:ros123" | chpasswd && \
    adduser rosadmin sudo

# Create a symlink so typing 'http-ros' runs the CLI manually (Control Plane only)
RUN echo '#!/bin/bash\ndotnet /app/HttpROS.HttpROS.dll --cli-only "$@"' > /usr/bin/http-ros && \
    chmod +x /usr/bin/http-ros

# Expose SSH port and standard web ports
EXPOSE 50022 80 443

# Start Script: Runs SSH in background and Proxy in foreground (so container stays alive)
RUN echo '#!/bin/bash\n/usr/sbin/sshd\ndotnet /app/HttpROS.HttpROS.dll' > /app/entrypoint.sh && \
    chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
