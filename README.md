# AdemirBot

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://github.com/welldtr/AdemirBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/dotnet.yml)
[![Docker Image CI](https://github.com/welldtr/AdemirBot/actions/workflows/docker-image.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/docker-image.yml)
[![Build Status](https://dev.azure.com/ademirbot/AdemirBot/_apis/build/status%2Fwelldtr.AdemirBot?branchName=production)](https://dev.azure.com/ademirbot/AdemirBot/_build/latest?definitionId=1&branchName=production)
[![SonarCloud](https://github.com/welldtr/AdemirBot/actions/workflows/sonarcloud.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/sonarcloud.yml)

## Descrição
O projeto "Ademir" é um bot criado para melhorar a experiência de comunidades focadas em bem-estar. Ele permite reproduzir músicas, criar macros, efetuar ações de moderação em massa e conversar com a API do ChatGPT.

## Dependências externas
Para utilizar todos os recursos desenvolvidos nesse projeto é necessário:
1. Criar um aplicativo no [Developer Portal do Discord](https://discord.com/developers/docs/getting-started)
2. Criar um aplicativo no [Developer Console do Spotify](https://developer.spotify.com/documentation/web-api/tutorials/getting-started)
3. Criar uma conta (paga) no OpenAI e criar uma [API Key](https://platform.openai.com/account/api-keys)
4. Criar uma instância MongoDB para o Bot guardar os dados de configuração

## Instalação (DevEnv)
Para utilizar o bot "Ademir" em seu servidor do Discord, siga as etapas abaixo:
1. Clone este repositório em sua máquina local.
2. Instale as dependências necessárias executando o comando `dotnet restore`.
3. Defina as seguintes varáveis de ambiente:
   - `SpotifyApiClientId`: Client ID do Aplicativo Spotify.
   - `SpotifyApiClientSecret`: Client Secret do Aplicativo Spotify
   - `PremiumGuilds`: IDs dos Servers permitidos para utilizar o ChatGPT
   - `AdemirAuth`: Token de autenticação do bot do Discord
   - `MongoServer`: String de conexão do Mongo DB
   - `ChatGPTKey`: Token de autenticação da conta de API do ChatGPT
4. Execute o bot utilizando o comando `dotnet run`.

## Instalação (Docker)
Rode o seguintes comandos para iniciar o Ademir no docker:

Para construir a imagem:
```sh
docker build -t ademir .
```

Para iniciar o container:
```sh
docker run -e SpotifyApiClientId=<Client ID do Aplicativo Spotify> \
           -e SpotifyApiClientSecret=<Client Secret do Aplicativo Spotify> \
           -e PremiumGuilds=<IDs dos Servers permitidos para utilizar o ChatGPT> \
           -e AdemirAuth=<Token de autenticação do bot do Discord> \
           -e MongoServer=<String de conexão do Mongo DB> \
           -e ChatGPTKey=<Token de autenticação da conta de API do ChatGPT> \
           ademir
```

## Comandos
- `>>play <link/track/playlist/album>`: Reproduz uma música, playlist ou álbum.
- `>>skip`: Pula para a próxima música da fila.
- `>>pause`: Pausa a reprodução da música atual.
- `>>stop`: Interrompe completamente a reprodução de música.
- `>>queue`: Lista as próximas 20 músicas da fila.
- `>>quit`: Remove o bot da chamada de voz.
- `>>volume <valor>`: Ajusta o volume da música.

## Licença
Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE.txt](LICENSE.txt) para obter mais informações.

## Contato
Se você tiver alguma dúvida ou sugestão sobre o projeto "Ademir", sinta-se à vontade para entrar em contato:
- [Discord](https://discord.gg/invite/Q6fQrf5jWX)
