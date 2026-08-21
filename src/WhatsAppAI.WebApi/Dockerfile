FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/WhatsAppAI.WebApi/WhatsAppAI.WebApi.csproj", "src/WhatsAppAI.WebApi/"]
COPY ["src/WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj", "src/WhatsAppAI.Infrastructure/"]
COPY ["src/WhatsAppAI.Application/WhatsAppAI.Application.csproj", "src/WhatsAppAI.Application/"]
COPY ["src/WhatsAppAI.Domain/WhatsAppAI.Domain.csproj", "src/WhatsAppAI.Domain/"]
RUN dotnet restore "src/WhatsAppAI.WebApi/WhatsAppAI.WebApi.csproj"
COPY src/ src/
WORKDIR "/src/src/WhatsAppAI.WebApi"
RUN dotnet build "WhatsAppAI.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WhatsAppAI.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false
WORKDIR /src
RUN dotnet tool restore
RUN dotnet ef migrations bundle --project src/WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj --startup-project src/WhatsAppAI.WebApi/WhatsAppAI.WebApi.csproj --output /app/publish/efbundle

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WhatsAppAI.WebApi.dll"]
