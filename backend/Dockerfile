# One build, two images:
#   docker build --target api    -t showcase-api    backend
#   docker build --target worker -t showcase-worker backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Showcase.slnx ./
COPY src/ src/
RUN dotnet publish src/Showcase.Api -c Release -o /out/api \
 && dotnet publish src/Showcase.Worker -c Release -o /out/worker

# Folders the app user can write to; named volumes mounted here inherit the ownership.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN mkdir -p /data/uploads /data/tus /data/keys && chown -R $APP_UID /data
ENV Storage__Root=/data/uploads \
    Upload__TempPath=/data/tus \
    DataProtection__KeysPath=/data/keys
USER $APP_UID
WORKDIR /app

FROM base AS api
COPY --from=build /out/api .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Showcase.Api.dll"]

# gltf-transform (Node) for the optimize step: installed here, copied into the worker.
FROM node:22-bookworm-slim AS gltf
RUN npm install -g --prefix /opt/gltf @gltf-transform/cli@4 \
 && /opt/gltf/bin/gltf-transform --version

# Worker = .NET runtime + Blender (portable Linux build, for .fbx → .glb) + node/gltf-transform.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
ARG BLENDER_VERSION=4.2.3
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl xz-utils \
      libx11-6 libxi6 libxxf86vm1 libxfixes3 libxrender1 libxext6 libxkbcommon0 libsm6 libice6 libgl1 libegl1 \
 && curl -fsSL "https://download.blender.org/release/Blender${BLENDER_VERSION%.*}/blender-${BLENDER_VERSION}-linux-x64.tar.xz" \
      | tar -xJ -C /opt \
 && mv "/opt/blender-${BLENDER_VERSION}-linux-x64" /opt/blender \
 && ln -s /opt/blender/blender /usr/local/bin/blender \
 && apt-get purge -y curl xz-utils && apt-get autoremove -y && rm -rf /var/lib/apt/lists/* \
 && blender -b --factory-startup --version
COPY --from=gltf /usr/local/bin/node /usr/local/bin/node
COPY --from=gltf /opt/gltf /opt/gltf
RUN ln -s /opt/gltf/bin/gltf-transform /usr/local/bin/gltf-transform && gltf-transform --version \
 && mkdir -p /data/uploads /data/work && chown -R $APP_UID /data
ENV Storage__Root=/data/uploads \
    Pipeline__WorkDir=/data/work
USER $APP_UID
WORKDIR /app
COPY --from=build /out/worker .
ENTRYPOINT ["dotnet", "Showcase.Worker.dll"]
