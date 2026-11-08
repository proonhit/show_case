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

# GĐ2 adds Blender and gltf-transform to this image.
FROM base AS worker
COPY --from=build /out/worker .
ENTRYPOINT ["dotnet", "Showcase.Worker.dll"]
