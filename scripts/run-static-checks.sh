#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [ -n "${CI:-}" ]; then
  git diff --check HEAD^! -- '*.cs' '*.js' '*.json' '*.yml' '*.yaml' '*.md' '*.sh' '*.asmdef' '.releaserc.yml'
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
      guardStack.push(/^\s*#if\s+(?:UNITY_EDITOR|!COMPILER_UDONSHARP\s*&&\s*UNITY_EDITOR|UNITY_EDITOR\s*&&\s*!COMPILER_UDONSHARP)\s*$/.test(line));
      continue;
    }
    if (/^\s*#endif\b/.test(line)) {
      guardStack.pop();
      continue;
    }
    if (/^\s*#else\b/.test(line)) {
      if (guardStack.length > 0) guardStack[guardStack.length - 1] = false;
      continue;
    }
    if (/^\s*#elif\b/.test(line)) {
      if (guardStack.length > 0) {
        guardStack[guardStack.length - 1] =
          /^\s*#elif\s+(?:UNITY_EDITOR|!COMPILER_UDONSHARP\s*&&\s*UNITY_EDITOR|UNITY_EDITOR\s*&&\s*!COMPILER_UDONSHARP)\s*$/.test(line);
      }
      continue;
    }
    if (!guardStack.includes(true) && /^\s*using (UnityEditor|UdonSharpEditor);$/.test(line)) {
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
