#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT_DIR/mac"
DIST_DIR="$ROOT_DIR/dist"
APP_NAME="反重力汉化开关.app"
APP_DIR="$DIST_DIR/$APP_NAME"
ARCH="${1:-$(uname -m)}"

NODE_VERSION="v22.14.0"
if [[ "$ARCH" == "arm64" ]]; then
  NODE_ARCH="darwin-arm64"
else
  NODE_ARCH="darwin-x64"
fi

CACHE_NODE="$ROOT_DIR/.cache/node"
if [[ ! -x "$CACHE_NODE" ]]; then
  mkdir -p "$ROOT_DIR/.cache"
  echo "Downloading Node.js $NODE_VERSION ($NODE_ARCH)..."
  TAR_URL="https://nodejs.org/dist/${NODE_VERSION}/node-${NODE_VERSION}-${NODE_ARCH}.tar.gz"
  curl -fsSL "$TAR_URL" -o "$ROOT_DIR/.cache/node.tar.gz"
  tar -xzf "$ROOT_DIR/.cache/node.tar.gz" -C "$ROOT_DIR/.cache" "node-${NODE_VERSION}-${NODE_ARCH}/bin/node"
  mv "$ROOT_DIR/.cache/node-${NODE_VERSION}-${NODE_ARCH}/bin/node" "$CACHE_NODE"
  rm -rf "$ROOT_DIR/.cache/node-${NODE_VERSION}-${NODE_ARCH}" "$ROOT_DIR/.cache/node.tar.gz"
  chmod +x "$CACHE_NODE"
fi

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources/runtime"

echo "Compiling SwiftUI app..."
swiftc -O \
  -parse-as-library \
  -target "${ARCH}-apple-macos13.0" \
  "$MAC_DIR/AntigravityCnSwitch.swift" \
  -o "$APP_DIR/Contents/MacOS/AntigravityCnSwitch"

cp "$MAC_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"
cp -R "$ROOT_DIR/patch" "$APP_DIR/Contents/Resources/patch"
cp "$CACHE_NODE" "$APP_DIR/Contents/Resources/runtime/node"
chmod +x "$APP_DIR/Contents/Resources/runtime/node"

echo "Ad-hoc signing app bundle..."
codesign --force --deep --sign - "$APP_DIR"

echo "Built: $APP_DIR"
