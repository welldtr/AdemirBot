FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /App

COPY . ./

RUN dotnet restore

RUN dotnet publish -c Release -o out
FROM mcr.microsoft.com/dotnet/aspnet:6.0
RUN apt-get update && \
    apt-get install -y --no-install-recommends libopus-dev libsodium-dev ffmpeg libfreetype6 libfontconfig1
WORKDIR /App
COPY --from=build-env /App/out .
COPY --from=build-env /App/out/shared/fonts /usr/share/fonts
ENV TZ=America/Sao_Paulo
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
ENTRYPOINT ["dotnet", "DiscordBot.dll"]