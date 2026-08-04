FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    ca-certificates \
    libstdc++6 \
    libgcc-s1 \
    libc6 \
    libgl1 \
    libx11-6 \
    libxcursor1 \
    libxrandr2 \
    libxi6 \
    libxinerama1 \
    libasound2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY Builds/ServerLinux/ ./

RUN chmod +x ./MmoPocServer

EXPOSE 7777

CMD ["./MmoPocServer", "-batchmode", "-nographics", "-logFile", "-"]