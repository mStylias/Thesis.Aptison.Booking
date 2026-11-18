# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.203-noble AS restore
WORKDIR /source

COPY src/Aptison.Booking.Domain/Aptison.Booking.Domain.csproj ./Aptison.Booking.Api/src/Aptison.Booking.Domain/
COPY src/Aptison.Booking.Application/Aptison.Booking.Application.csproj ./Aptison.Booking.Api/src/Aptison.Booking.Application/
COPY src/Aptison.Booking.Infrastructure/Aptison.Booking.Infrastructure.csproj ./Aptison.Booking.Api/src/Aptison.Booking.Infrastructure/
COPY src/Aptison.Booking.Api/Aptison.Booking.Api.csproj ./Aptison.Booking.Api/src/Aptison.Booking.Api/

COPY --from=synax src/Libraries/Synax.AspNetCore/Synax.AspNetCore.csproj ./Synax/src/Libraries/Synax.AspNetCore/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore/Synax.EntityFrameworkCore.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore.AspNetCore/Synax.EntityFrameworkCore.AspNetCore.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore.AspNetCore/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore.Mapper/Synax.EntityFrameworkCore.Mapper.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore.Mapper/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore.Mediator/Synax.EntityFrameworkCore.Mediator.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore.Mediator/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore.OData/Synax.EntityFrameworkCore.OData.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore.OData/
COPY --from=synax src/Libraries/Synax.EntityFrameworkCore.OData.Results/Synax.EntityFrameworkCore.OData.Results.csproj ./Synax/src/Libraries/Synax.EntityFrameworkCore.OData.Results/
COPY --from=synax src/Libraries/Synax.Mapper/Synax.Mapper.csproj ./Synax/src/Libraries/Synax.Mapper/
COPY --from=synax src/Libraries/Synax.Mediator/Synax.Mediator.csproj ./Synax/src/Libraries/Synax.Mediator/
COPY --from=synax src/Libraries/Synax.Modules/Synax.Modules.csproj ./Synax/src/Libraries/Synax.Modules/
COPY --from=synax src/Libraries/Synax.Results/Synax.Results.csproj ./Synax/src/Libraries/Synax.Results/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./Aptison.Booking.Api/src/Aptison.Booking.Api/Aptison.Booking.Api.csproj

FROM restore AS publish
ARG BUILD_CONFIGURATION=Release
COPY . ./Aptison.Booking.Api/
COPY --from=synax src/Libraries ./Synax/src/Libraries/
RUN find ./Synax -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./Aptison.Booking.Api/src/Aptison.Booking.Api/Aptison.Booking.Api.csproj
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish ./Aptison.Booking.Api/src/Aptison.Booking.Api/Aptison.Booking.Api.csproj \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.7-noble-chiseled AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=publish /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "Aptison.Booking.Api.dll"]


# Debuggable variant. Not chiseled and not rootless: Rider lists a container's processes by
# exec'ing `ps` into it, and chiseled ships neither a shell nor procps — hence "no processes
# to attach".
FROM mcr.microsoft.com/dotnet/aspnet:10.0.7-noble AS debug
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends procps \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish ./

ENTRYPOINT ["dotnet", "Aptison.Booking.Api.dll"]
