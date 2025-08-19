FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY FileUploadApi/FileUploadApi.csproj FileUploadApi/
RUN dotnet restore "FileUploadApi/FileUploadApi.csproj"

# Copy the entire source code
COPY . .

# Build and publish the specific project
WORKDIR "/src/FileUploadApi"
RUN dotnet publish "FileUploadApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FileUploadApi.dll"]