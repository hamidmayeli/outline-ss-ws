FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install OpenSSL for certificate generation
RUN apt-get update && apt-get install -y openssl

# Generate and trust the developer certificate
RUN dotnet dev-certs https --clean
RUN mkdir -p /root/.aspnet/https/
RUN dotnet dev-certs https -ep /root/.aspnet/https/aspnetapp.pfx -p password
RUN dotnet dev-certs https --trust

WORKDIR /sln

COPY ./backend/OutlineManager.slnx ./
COPY ./backend/API/*.csproj ./API/
COPY ./backend/API.Tests/*.csproj ./API.Tests/

RUN dotnet restore

COPY ./backend .

RUN dotnet build -c Release

FROM build AS publish

# Install native build dependencies for Native AOT
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    clang zlib1g-dev libkrb5-dev

RUN dotnet publish ./API/API.csproj -r linux-x64 -c Release -o /app/publish

FROM build AS test-api
WORKDIR /sln
RUN dotnet test ./OutlineManager.slnx

# ============== Build Client  ============== 
FROM node:25-slim AS build-client

RUN apt-get update && apt-get install jq -y
RUN npm install -g pnpm

WORKDIR /app

COPY ./frontend/package.json ./
COPY ./frontend/pnpm-lock.yaml ./
RUN pnpm install

COPY ./frontend .
# RUN npm run lint && npm run build
RUN pnpm run build

FROM ubuntu AS final

WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build-client /app/dist ./wwwroot

# Copy the certificate to the final image
COPY --from=build /root/.aspnet/https /root/.aspnet/https

ENV ASPNETCORE_HTTP_PORTS=80
ENV ASPNETCORE_HTTPS_PORTS=443
EXPOSE 80
EXPOSE 443

# Set the environment variable for the certificate
# ENV ASPNETCORE_Kestrel__Certificates__Default__Password=password
# ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/root/.aspnet/https/aspnetapp.pfx

ENTRYPOINT ["./API"]
