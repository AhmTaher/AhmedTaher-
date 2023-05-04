#!/bin/bash
die () {
    echo "$*" >&2
    exit 1
}

make_absolute () {
    case "$1" in
    /*)
        echo "$1"
        ;;
    *)
        echo "$PWD/$1"
        ;;
    esac
}

# Parse script arguments
for i in "$@"
do
case "$i" in
    --configuration=*)
    CONFIGURATION="${i#*=}"
    shift # past argument=value
    ;;
    *)
          # unknown option
    ;;
esac
done

# Directories
THISDIR="$( cd "$(dirname "$0")" ; pwd -P )"
ROOT="$( cd "$THISDIR"/../../.. ; pwd -P )"
SRC="$ROOT/src"
OUT="$ROOT/out"
GCM_SRC="$SRC/shared/Git-Credential-Manager"
PROJ_OUT="$OUT/linux/Packaging.Linux"

# Build parameters
FRAMEWORK=net6.0

# Determine a runtime if one was not provided
# NB: Unsure of the arm64 vs aarch64, but uname reports aarch64 whereas 
# dotnet seems to want arm64 ...
if [ -z "$RUNTIME" ]; then
    TEST_RUNTIME=`uname -m`
    case $TEST_RUNTIME in
        "x86_64")
            RUNTIME="linux-x64"
            ;;
        "arm64")
            RUNTIME="linux-arm64"
            ;;
        "aarch64")
            RUNTIME="linux-arm64"
            ;;
        *)
            die "Unknown runtime '$TEST_RUNTIME'"
            ;;
    esac
fi

echo "Building for runtime '$RUNTIME'"

# Perform pre-execution checks
CONFIGURATION="${CONFIGURATION:=Debug}"

# Outputs
PAYLOAD="$PROJ_OUT/$CONFIGURATION/payload"
SYMBOLOUT="$PROJ_OUT/$CONFIGURATION/payload.sym"

# Cleanup payload directory
if [ -d "$PAYLOAD" ]; then
    echo "Cleaning existing payload directory '$PAYLOAD'..."
    rm -rf "$PAYLOAD"
fi

# Cleanup symbol directory
if [ -d "$SYMBOLOUT" ]; then
    echo "Cleaning existing symbols directory '$SYMBOLOUT'..."
    rm -rf "$SYMBOLOUT"
fi

# Ensure directories exists
mkdir -p "$PAYLOAD" "$SYMBOLOUT"

if [ -z "$DOTNET_ROOT" ]; then
    DOTNET_ROOT="$(dirname $(which dotnet))"
fi

# Publish core application executables
echo "Publishing core application..."
$DOTNET_ROOT/dotnet publish "$GCM_SRC" \
	--configuration="$CONFIGURATION" \
	--framework="$FRAMEWORK" \
	--runtime="$RUNTIME" \
	--self-contained \
	-p:PublishSingleFile=true \
	--output="$(make_absolute "$PAYLOAD")" || exit 1

# Collect symbols
echo "Collecting managed symbols..."
mv "$PAYLOAD"/*.pdb "$SYMBOLOUT" || exit 1

echo "Build complete."
