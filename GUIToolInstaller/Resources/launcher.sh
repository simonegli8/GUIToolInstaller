#!/bin/bash
export DOTNET_ROOT="/usr/local/share/dotnet"
export PATH="$HOME/.dotnet/tools:$PATH"
exec "$HOME/.dotnet/tools/{{AppName}}" "$@"