# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "MuuBoi.dll"]

# docker run -p 8080:8080 \
#   -e "ConnectionStrings__DefaultConnection=Server=...;Database=...;" \
#   -e "Jwt__Key=..." \
#   muuboi