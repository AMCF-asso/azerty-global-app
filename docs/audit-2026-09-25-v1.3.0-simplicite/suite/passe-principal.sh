#!/usr/bin/env bash
# Une passe des deux bancs dans le checkout principal (build Release déjà fait).
#   bash passe-principal.sh DOSSIER_SORTIE
set -u
DEPOT="D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
export AZERTY_CAPTURE=1
export AZERTY_CONTEXTE=1
export AZERTY_CAPTURE_DIR="$1"
mkdir -p "$1"
cd "$DEPOT" || exit 2
dotnet test src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~CaptureBench|FullyQualifiedName~KeyboardContextBench" 2>&1 | grep -E "Passed!|Failed!|Could not load|0x800711C7" | head -3
echo "PNG : $(ls "$1"/*.png 2>/dev/null | wc -l)"
