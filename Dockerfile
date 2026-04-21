# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app .

# Install dependencies: SSH server, Nginx (for HttpROS to manage), and sudo
RUN apt-get update && apt-get install -y \
    openssh-server \
    nginx \
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

# Create a symlink so typing 'http-ros' runs the application
RUN echo '#!/bin/bash\ndotnet /app/HttpROS.dll "$@"' > /usr/bin/http-ros && \
    chmod +x /usr/bin/http-ros

# Expose SSH port and standard Nginx ports
EXPOSE 50022 80 443

# Start SSH and Nginx (keeping the container running)
CMD service nginx start && /usr/sbin/sshd -D
