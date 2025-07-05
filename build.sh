#!/bin/bash

PATH_SCRIPT="$(cd "${0%/*}" && echo "$PWD")"
printf "Current location: '%s'\n" "$PATH_SCRIPT"

# -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishSingleFile=true

DEBUG_FLAGS="-c Debug"

dotnet publish $DEBUG_FLAGS -r linux-x64 --self-contained true *.sln &&
	dotnet publish $DEBUG_FLAGS -r win-x64 --self-contained true *.sln
