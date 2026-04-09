#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [ -n "${CI:-}" ]; then
  CHECK_PATHS=('*.cs' '*.js' '*.json' '*.yml' '*.yaml' '*.md' '*.sh' '*.asmdef' '.releaserc.yml')
  if git rev-parse --verify HEAD^2 >/dev/null 2>&1; then
    TARGET_COMMIT='HEAD^2'
  else
    TARGET_COMMIT='HEAD'
  fi
  mapfile -t CHECK_FILES < <(git diff-tree --no-commit-id --name-only --root -r "$TARGET_COMMIT" -- "${CHECK_PATHS[@]}")
  if [ "${#CHECK_FILES[@]}" -gt 0 ]; then
    git diff-tree --check --no-commit-id --root -r "$TARGET_COMMIT" -- "${CHECK_FILES[@]}"
  fi
else
  git diff --check
fi

ruby -e 'if File.exist?(".releaserc.yml"); require "yaml"; YAML.load_file(".releaserc.yml"); end; Dir[".github/workflows/*.yml"].sort.each { |path| require "yaml"; YAML.load_file(path) }'

node <<'NODE'
const fs = require("fs");
const path = require("path");

const jsonFiles = ["package.json", "package-lock.json"];
for (const file of jsonFiles) {
  const target = path.join(process.cwd(), file);
  if (fs.existsSync(target)) JSON.parse(fs.readFileSync(target, "utf8"));
}

function walk(dir, predicate, out) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(fullPath, predicate, out);
    } else if (predicate(fullPath)) {
      out.push(fullPath);
    }
  }
}

const packageRoot = path.join(process.cwd(), "Packages");
const packageJsons = [];
const asmdefs = [];
if (fs.existsSync(packageRoot)) {
  walk(packageRoot, file => file.endsWith("package.json"), packageJsons);
  walk(packageRoot, file => file.endsWith(".asmdef"), asmdefs);
}

for (const file of [...packageJsons, ...asmdefs]) {
  JSON.parse(fs.readFileSync(file, "utf8"));
}

const errors = [];
const csFiles = [];
if (fs.existsSync(packageRoot)) {
  walk(packageRoot, file => file.endsWith('.cs'), csFiles);
}
for (const fullPath of csFiles) {
  const relativePath = path.relative(process.cwd(), fullPath).replaceAll(path.sep, "/");
  if (relativePath.includes('/Editor/')) continue;
  const source = fs.readFileSync(fullPath, 'utf8');
  const lines = source.split(/\r?\n/);
  const guardStack = [];
  for (const line of lines) {
    if (/^\s*#if\b/.test(line)) {
      const matchesEditorOnly =
        /^\s*#if\s+(?:UNITY_EDITOR|!COMPILER_UDONSHARP\s*&&\s*UNITY_EDITOR|UNITY_EDITOR\s*&&\s*!COMPILER_UDONSHARP)\s*$/.test(line);
      guardStack.push({ active: matchesEditorOnly, matched: matchesEditorOnly });
      continue;
    }
    if (/^\s*#endif\b/.test(line)) {
      guardStack.pop();
      continue;
    }
    if (/^\s*#else\b/.test(line)) {
      if (guardStack.length > 0) {
        const current = guardStack[guardStack.length - 1];
        current.active = !current.matched;
        current.matched = true;
      }
      continue;
    }
    if (/^\s*#elif\b/.test(line)) {
      if (guardStack.length > 0) {
        const current = guardStack[guardStack.length - 1];
        const matchesEditorOnly =
          /^\s*#elif\s+(?:UNITY_EDITOR|!COMPILER_UDONSHARP\s*&&\s*UNITY_EDITOR|UNITY_EDITOR\s*&&\s*!COMPILER_UDONSHARP)\s*$/.test(line);
        current.active = !current.matched && matchesEditorOnly;
        current.matched = current.matched || matchesEditorOnly;
      }
      continue;
    }
    if (!guardStack.some(frame => frame.active) && /^\s*using (UnityEditor|UdonSharpEditor);$/.test(line)) {
      errors.push(`${relativePath}: runtime script references editor-only namespaces without an editor preprocessor guard.`);
      break;
    }
  }
}

if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exit(1);
}
NODE
