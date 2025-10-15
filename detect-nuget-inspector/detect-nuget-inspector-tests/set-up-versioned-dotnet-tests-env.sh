#!/bin/bash

set -e

DOTNET6_URL="https://download.visualstudio.microsoft.com/download/pr/2e2e2e2e-2e2e-2e2e-2e2e-2e2e2e2e2e2e/abcdef1234567890abcdef1234567890/dotnet-sdk-6.0.420-osx-arm64.tar.gz"
DOTNET7_URL="https://download.visualstudio.microsoft.com/download/pr/3f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f/abcdef1234567890abcdef1234567890/dotnet-sdk-7.0.408-osx-arm64.tar.gz"
DOTNET8_URL="https://download.visualstudio.microsoft.com/download/pr/4a4a4a4a-4a4a-4a4a-4a4a-4a4a4a4a4a4a/abcdef1234567890abcdef1234567890/dotnet-sdk-8.0.204-osx-arm64.tar.gz"

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
