FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["YoutubeDownloader/YoutubeDownloader.csproj", "YoutubeDownloader/"]
RUN dotnet restore "YoutubeDownloader/YoutubeDownloader.csproj"

COPY YoutubeDownloader/ YoutubeDownloader/
RUN dotnet publish "YoutubeDownloader/YoutubeDownloader.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg python3 curl \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5232
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5232
ENTRYPOINT ["dotnet", "YoutubeDownloader.dll"]