# AdemirBot

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://github.com/welldtr/AdemirBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/dotnet.yml)
[![Docker Image CI](https://github.com/welldtr/AdemirBot/actions/workflows/docker-image.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/docker-image.yml)
[![Build Status](https://dev.azure.com/ademirbot/AdemirBot/_apis/build/status%2Fwelldtr.AdemirBot?branchName=production)](https://dev.azure.com/ademirbot/AdemirBot/_build/latest?definitionId=1&branchName=production)
[![SonarCloud](https://github.com/welldtr/AdemirBot/actions/workflows/sonarcloud.yml/badge.svg)](https://github.com/welldtr/AdemirBot/actions/workflows/sonarcloud.yml)

## Descrição
O projeto "Ademir" é um bot criado para melhorar a experiência de comunidades focadas em bem-estar. Ele permite reproduzir músicas, criar macros, efetuar ações de moderação em massa e conversar com a API do ChatGPT.

## Funcionalidades
- :white_check_mark: Falar com o bot apenas mencionando o mesmo
- :white_check_mark: Reprodução/download de músicas/playlists em canal de audio
- :white_check_mark: Suporte a links de vídeo do YouTube
- :white_check_mark: Suporte a links de musicas do Spotify
- :white_check_mark: Suporte a links de albuns do Spotify
- :white_check_mark: Suporte a links de playlists públicas do Spotify
- :white_check_mark: Suporte a links de playlists públicas do YouTube
- :white_check_mark: Denunciar um usuário através do comando `/denunciar`
- :white_check_mark: Denunciar uma mensagem com o menu de contexto

## Comandos de Booster
- :white_check_mark: Falar com o bot em uma thread com o comando `/thread`
- :white_check_mark: Gerar imagens com o comando `/dall-e`
- :white_check_mark: Gerar texto com o comando `/completar`

## Comandos do Administrador
- :white_check_mark: Configurar o Canal de Denúncias: Comando `/config-denuncias`
- :white_check_mark: Criar macros através do comando `/macro`
- :white_check_mark: Editar macros: comando `/editar-macro`
- :white_check_mark: Excluir macro: comando `/excluir-macro`
- :white_check_mark: Banir em massa: comando `/massban`
- :white_check_mark: Expulsar em massa: comando `/masskick`
- :white_check_mark: Importar histórico de mensagens: comando `/importar-historico-mensagens`
- :white_check_mark: Extrair lista de usuarios por atividade no servidor `/usuarios-inativos`
- :white_check_mark: Configurar cargo extra para falar com o bot: comando `/config-cargo-ademir`

## Comandos de Música
- :white_check_mark: `>>play <link/track/playlist/album>`: Reproduz uma música, playlist ou álbum.
- :white_check_mark: `>>skip`: Pula para a próxima música da fila.
- :white_check_mark: `>>back`: Pula para a música anterior da fila.
- :white_check_mark: `>>replay`: Reinicia a música atual.
- :white_check_mark: `>>pause`: Pausa/Retoma a reprodução da música atual.
- :white_check_mark: `>>stop`: Interrompe completamente a reprodução de música.
- :white_check_mark: `>>loop`: Habilita/Desabilita o modo de repetição de faixa.
- :white_check_mark: `>>loopqueue`: Habilita/Desabilita o modo de repetição de playlist.
- :white_check_mark: `>>queue`: Lista as próximas 20 músicas da fila.
- :white_check_mark: `>>join`: Puxa o bot para o seu canal de voz.
- :white_check_mark: `>>quit`: Remove o bot da chamada de voz.
- :white_check_mark: `>>volume <valor>`: Ajusta o volume da música.

## Instalação (DevEnv)

### Dependências externas
Para utilizar todos os recursos desenvolvidos nesse projeto é necessário:
1. Criar um aplicativo no [Developer Portal do Discord](https://discord.com/developers/docs/getting-started)
2. Criar um aplicativo no [Developer Console do Spotify](https://developer.spotify.com/documentation/web-api/tutorials/getting-started)
3. Criar uma conta (paga) no OpenAI e criar uma [API Key](https://platform.openai.com/account/api-keys)
4. Criar uma instância MongoDB para o Bot guardar os dados de configuração

### Passo a passo
Para utilizar o bot "Ademir" em seu servidor do Discord, siga as etapas abaixo:
1. Clone este repositório em sua máquina local.
2. Instale as dependências necessárias executando o comando `pip install -r requirements.txt`.
3. Defina as seguintes varáveis de ambiente:
   - `SpotifyApiClientId`: Client ID do Aplicativo Spotify.
   - `SpotifyApiClientSecret`: Client Secret do Aplicativo Spotify
   - `PremiumGuilds`: IDs dos Servers permitidos para utilizar o ChatGPT
   - `AdemirAuth`: Token de autenticação do bot do Discord
   - `MongoServer`: String de conexão do Mongo DB
   - `ChatGPTKey`: Token de autenticação da conta de API do ChatGPT
4. Execute o bot utilizando o comando `python main.py`.

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

## Licença
Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE.txt](LICENSE.txt) para obter mais informações.

## Contato
Se você tiver alguma dúvida ou sugestão sobre o projeto "Ademir", sinta-se à vontade para entrar em contato:
- [Discord](https://discord.gg/invite/Q6fQrf5jWX)
