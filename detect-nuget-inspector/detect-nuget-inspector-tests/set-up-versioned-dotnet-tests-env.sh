#!/bin/bash

set -e
# Your version of the following may differ in minor version numbers. The test are written to align with the Jenkins 
# build machine (ubuntu 20.04 at time of writing. Adjust as needed. 
DOTNET6_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/6.0.428/dotnet-sdk-6.0.428-osx-arm64.pkg"
DOTNET7_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/7.0.410/dotnet-sdk-7.0.410-osx-arm64.pkg"
DOTNET8_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.121/dotnet-sdk-8.0.121-osx-arm64.pkg"

install_dotnet() {
  VERSION=$1
  URL=$2
  DEST="$HOME/.dotnet$VERSION"
  BIN="/usr/local/bin/dotnet$VERSION"

  echo "Downloading .NET $VERSION..."
  rm -rf "$DEST"
  mkdir -p "$DEST"
  if curl -L "$URL" | tar -xz -C "$DEST"; then
    echo "Downloaded and extracted .NET $VERSION."
  else
    echo "Failed to download or extract .NET $VERSION."
    exit 1
  fi

  echo "Setting up symlink for dotnet$VERSION..."
  if sudo rm -f "$BIN" && sudo ln -s "$DEST/dotnet" "$BIN"; then
    echo "Symlink for dotnet$VERSION set up."
  else
    echo "Failed to set up symlink for dotnet$VERSION."
    exit 1
  fi

  if "$BIN" --version; then
    echo ".NET $VERSION installed successfully."
  else
    echo "Failed to verify .NET $VERSION installation."
    exit 1
  fi
}

install_dotnet 6 "$DOTNET6_URL"
install_dotnet 7 "$DOTNET7_URL"
install_dotnet 8 "$DOTNET8_URL"

echo "All .NET SDKs installed and symlinked as dotnet6, dotnet7, dotnet8."
