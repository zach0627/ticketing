import test from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import ts from 'typescript';

const root = resolve(import.meta.dirname, '../src');
const walk = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory()
      ? walk(resolve(dir, entry.name))
      : /\.tsx?$/.test(entry.name)
        ? [resolve(dir, entry.name)]
        : [],
  );
const files = walk(root);
const imports = files.flatMap((file) => {
  const source = ts.createSourceFile(
    file,
    readFileSync(file, 'utf8'),
    ts.ScriptTarget.Latest,
    true,
  );
  const entries = [];
  function visit(node) {
    let specifier;
    let typeOnly = false;
    if (
      (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) &&
      node.moduleSpecifier
    ) {
      specifier = node.moduleSpecifier.text;
      typeOnly = node.isTypeOnly || node.importClause?.isTypeOnly || false;
    } else if (
      ts.isCallExpression(node) &&
      node.expression.kind === ts.SyntaxKind.ImportKeyword
    ) {
      specifier = node.arguments[0]?.text;
    }
    if (specifier) {
      const candidate = resolve(dirname(file), specifier);
      const target = specifier.startsWith('.')
        ? [
            candidate,
            `${candidate}.ts`,
            `${candidate}.tsx`,
            `${candidate}/index.ts`,
          ].find((p) => files.includes(p))
        : undefined;
      entries.push({
        file,
        source: relative(root, file),
        specifier,
        target,
        typeOnly,
      });
    }
    ts.forEachChild(node, visit);
  }
  visit(source);
  return entries;
});

test('feature pages/components use hooks; HTTP and query orchestration stay outside presentation', () => {
  for (const entry of imports) {
    if (
      entry.typeOnly ||
      !/^features\/[^/]+\/(pages|components|guards|context)\//.test(
        entry.source,
      )
    )
      continue;
    assert.ok(
      !entry.specifier.includes('/api/') &&
        entry.specifier !== '@tanstack/react-query',
      `${entry.source} must delegate ${entry.specifier} to a hook`,
    );
  }
});

test('shared stays independent; models have no UI or transport runtime dependencies', () => {
  for (const entry of imports) {
    const target = entry.target && relative(root, entry.target);
    if (entry.source.startsWith('shared/')) {
      assert.ok(
        !target || target.startsWith('shared/'),
        `${entry.source} depends on ${target}`,
      );
    }
    if (/^features\/[^/]+\/model\//.test(entry.source) && !entry.typeOnly) {
      assert.ok(
        target?.includes('/model/'),
        `${entry.source} has impure dependency ${entry.specifier}`,
      );
    }
    if (/^features\/[^/]+\/(hooks|api|model)\//.test(entry.source)) {
      assert.ok(
        !target ||
          !/\/(pages|components|context|guards)\//.test(target) ||
          (entry.source.startsWith('features/auth/hooks/') &&
            target.startsWith('features/auth/context/')),
        `${entry.source} reverses the dependency on ${target}`,
      );
      assert.ok(
        !entry.specifier.endsWith('.css'),
        `${entry.source} imports presentation styles`,
      );
    }
  }
});

test('cross-feature imports use public entries; only the router imports other feature pages', () => {
  for (const entry of imports) {
    if (!entry.target) continue;
    const target = relative(root, entry.target);
    if (!target.startsWith('features/')) continue;
    const own = entry.source.startsWith('features/')
      ? entry.source.split('/')[1]
      : null;
    if (own === target.split('/')[1]) continue;
    assert.ok(
      /^features\/[^/]+\/index\.ts$/.test(target) ||
        (entry.source === 'app/router.tsx' &&
          /^features\/[^/]+\/pages\//.test(target)),
      `${entry.source} reaches into ${target}`,
    );
  }
});

test('runtime module graph has no cycles and relative imports resolve', () => {
  const graph = new Map(files.map((file) => [file, []]));
  for (const entry of imports) {
    if (entry.specifier.startsWith('.')) {
      assert.ok(
        entry.target ||
          (entry.specifier.endsWith('.css') &&
            existsSync(resolve(dirname(entry.file), entry.specifier))),
        `${entry.source}: unresolved ${entry.specifier}`,
      );
    }
    if (!entry.typeOnly && entry.target)
      graph.get(entry.file).push(entry.target);
  }
  const visited = new Set();
  function visit(file, stack) {
    assert.ok(
      !stack.includes(file),
      `Circular import: ${[...stack, file].map((p) => relative(root, p)).join(' → ')}`,
    );
    if (visited.has(file)) return;
    for (const child of graph.get(file)) visit(child, [...stack, file]);
    visited.add(file);
  }
  for (const file of files) visit(file, []);
});
