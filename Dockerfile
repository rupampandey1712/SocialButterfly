# syntax=docker/dockerfile:1

FROM node:22-alpine AS frontend-build
WORKDIR /src/Frontend

ARG VITE_API_ORIGIN=""
ENV VITE_API_ORIGIN=${VITE_API_ORIGIN}

COPY Frontend/package*.json ./
RUN npm ci

COPY Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY Backend/SocialMediaWeb.csproj Backend/
RUN dotnet restore Backend/SocialMediaWeb.csproj

COPY Backend/ Backend/
COPY --from=frontend-build /src/Frontend/dist/ Backend/wwwroot/

RUN dotnet publish Backend/SocialMediaWeb.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

LABEL org.opencontainers.image.title="SocialMediaWeb" \
      org.opencontainers.image.description="Combined React frontend and ASP.NET Core backend"

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=backend-build /app/publish .

ENTRYPOINT ["dotnet", "SocialMediaWeb.dll"]
