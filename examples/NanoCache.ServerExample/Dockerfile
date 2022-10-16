#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

EXPOSE 5566

ENV DEBUG_MODE false
ENV USE_CREDENTIALS true
ENV VALID_CREDENTIALS admin:123456

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["examples/NanoCache.ServerExample/NanoCache.ServerExample.csproj", "examples/NanoCache.ServerExample/"]
COPY ["src/NanoCache/NanoCache.csproj", "src/NanoCache/"]
RUN dotnet restore "examples/NanoCache.ServerExample/NanoCache.ServerExample.csproj"
COPY . .
WORKDIR "/src/examples/NanoCache.ServerExample"
RUN dotnet build "NanoCache.ServerExample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NanoCache.ServerExample.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NanoCache.ServerExample.dll"]