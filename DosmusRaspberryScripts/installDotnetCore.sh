#!/bin/bash

cd ~/

sudo apt-get install curl libunwind8 gettext

echo "Baixando .NET Core 2.1.x Runtime"
curl -sSL -o dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Runtime/release/2.1/dotnet-runtime-latest-linux-arm.tar.gz

echo "Extraindo instalação para /opt/dotnet"
sudo rm -r /opt/dotnet
sudo mkdir -p /opt/dotnet && sudo tar zxf dotnet.tar.gz -C /opt/dotnet

echo "Criando link simbolico para dotnet"
sudo ln -s /opt/dotnet/dotnet /usr/local/bin

echo "Limpando arquivos temporarios"
sudo rm dotnet.tar.gz

echo "Instalação finalizada."
dotnet --info
