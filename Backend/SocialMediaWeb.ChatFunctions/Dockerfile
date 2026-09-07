FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/SocialMediaWeb.ServiceDefaults/SocialMediaWeb.ServiceDefaults.csproj Backend/SocialMediaWeb.ServiceDefaults/
COPY Backend/SocialMediaWeb.csproj Backend/
COPY Backend/SocialMediaWeb.ChatFunctions/SocialMediaWeb.ChatFunctions.csproj Backend/SocialMediaWeb.ChatFunctions/
RUN dotnet restore Backend/SocialMediaWeb.ChatFunctions/SocialMediaWeb.ChatFunctions.csproj

COPY Backend/ Backend/
RUN dotnet publish Backend/SocialMediaWeb.ChatFunctions/SocialMediaWeb.ChatFunctions.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
WORKDIR /home/site/wwwroot
COPY --from=build /app/publish .
