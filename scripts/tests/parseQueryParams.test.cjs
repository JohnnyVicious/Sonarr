const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const sourceFile = path.resolve(
  __dirname,
  '../../frontend/src/Utilities/String/parseQueryParams.ts'
);
const outputDirectory = fs.mkdtempSync(
  path.join(os.tmpdir(), 'sonarr-frontend-tests-')
);

childProcess.execFileSync(
  process.execPath,
  [
    require.resolve('typescript/bin/tsc'),
    sourceFile,
    '--module',
    'commonjs',
    '--target',
    'ES2020',
    '--outDir',
    outputDirectory,
    '--skipLibCheck'
  ],
  {
    stdio: 'inherit'
  }
);

process.on('exit', () => {
  fs.rmSync(outputDirectory, { force: true, recursive: true });
});

const parseQueryParams = require(path.join(
  outputDirectory,
  'parseQueryParams.js'
)).default;

test('parses simple query parameters', () => {
  assert.deepEqual(parseQueryParams('name=sonarr&enabled=true'), {
    enabled: 'true',
    name: 'sonarr'
  });
});

test('keeps duplicate keys as arrays', () => {
  assert.deepEqual(parseQueryParams('tag=alpha&tag=beta'), {
    tag: ['alpha', 'beta']
  });
});

test('parses bracketed object keys', () => {
  assert.deepEqual(parseQueryParams('series[id]=10&series[title]=Test'), {
    series: {
      id: '10',
      title: 'Test'
    }
  });
});

test('parses bracketed array keys', () => {
  assert.deepEqual(parseQueryParams('tags[]=alpha&tags[]=beta'), {
    tags: ['alpha', 'beta']
  });
});

test('parses indexed arrays using qs array compaction', () => {
  assert.deepEqual(parseQueryParams('tags[15]=alpha&tags[16]=beta'), {
    tags: ['alpha', 'beta']
  });
});

test('parses nested array keys', () => {
  assert.deepEqual(parseQueryParams('filter[tags][]=alpha&filter[tags][]=beta'), {
    filter: {
      tags: ['alpha', 'beta']
    }
  });
});

test('decodes URL encoded keys and plus signs', () => {
  assert.deepEqual(parseQueryParams('series%20title=Breaking+Bad'), {
    'series title': 'Breaking Bad'
  });
});

test('ignores unsafe prototype keys', () => {
  assert.deepEqual(
    parseQueryParams('__proto__[polluted]=true&constructor[polluted]=true'),
    {}
  );
  assert.equal({}.polluted, undefined);
});
