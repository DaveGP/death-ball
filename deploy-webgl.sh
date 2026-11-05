#!/bin/bash
# Builds the WebGL version and publishes it to the gh-pages branch, which GitHub Pages serves at
# https://davegp.github.io/death-ball/. Close the Unity Editor first: batch mode can't open a project
# that is already open. Pass --skip-build to publish the existing Unity/Builds/WebGL as it is.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
BUILD="$ROOT/Unity/Builds/WebGL"

if [[ "${1:-}" != "--skip-build" ]]; then
	echo "Building WebGL (takes a few minutes)..."
	"$UNITY" -batchmode -projectPath "$ROOT/Unity" -executeMethod Builders.WebGLBuilder.BuildBatch -logFile "$ROOT/Unity/Logs/webgl.log"
fi

[[ -f "$BUILD/index.html" ]] || { echo "No build at $BUILD"; exit 1; }

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

git -C "$ROOT" fetch origin gh-pages 2>/dev/null || true

if git -C "$ROOT" show-ref --verify --quiet refs/remotes/origin/gh-pages; then
	git clone --quiet --branch gh-pages --single-branch "$(git -C "$ROOT" remote get-url origin)" "$STAGING"
else
	git -C "$STAGING" init --quiet -b gh-pages
	git -C "$STAGING" remote add origin "$(git -C "$ROOT" remote get-url origin)"
fi

# Keep earlier builds' hash-named files in Build/: browsers may hold a cached index.html for ten minutes
# that still points at them
find "$STAGING" -mindepth 1 -maxdepth 1 ! -name .git ! -name Build -exec rm -rf {} +
find "$STAGING/Build" -name 'WebGL.*' -delete 2>/dev/null || true
cp -R "$BUILD/." "$STAGING/"

# Pages serves LFS files as pointers, so keep every file of the site in plain git
printf '* !filter !diff !merge\n' > "$STAGING/.gitattributes"

git -C "$STAGING" add -A

if git -C "$STAGING" diff --cached --quiet; then
	echo "Nothing changed since the last deploy."
	exit 0
fi

git -C "$STAGING" commit --quiet -m "Deploy WebGL build from $(git -C "$ROOT" rev-parse --short HEAD)"
git -C "$STAGING" push --quiet origin gh-pages

echo "Published. The site updates at https://davegp.github.io/death-ball/ within a minute or two."
