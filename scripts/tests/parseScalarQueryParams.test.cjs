const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const sourceFile = path.resolve(
  __dirname,
  '../../frontend/src/Utilities/String/parseScalarQueryParams.ts'
);
const outputDirectory = fs.mkdtempSync(
  path.join(__dirname, '.compiled-')
);

process.on('exit', () => {
  fs.rmSync(outputDirectory, { force: true, recursive: true });
});

childProcess.execFileSync(
  process.execPath,
  [
    require.resolve('typescript/bin/tsc'),
    sourceFile,
    '--module',
    'commonjs',
    '--target',
    'ES2020',
    '--esModuleInterop',
    '--outDir',
    outputDirectory,
    '--skipLibCheck'
  ],
  {
    stdio: 'inherit'
  }
);

const parseScalarQueryParams = require(path.join(
  outputDirectory,
  'parseScalarQueryParams.js'
)).default;

test('parses scalar query parameters', () => {
  assert.deepEqual(parseScalarQueryParams('?term=Breaking+Bad&tvdbId=81189'), {
    term: 'Breaking Bad',
    tvdbId: '81189'
  });
});

test('keeps last duplicate value to preserve current hook behavior', () => {
  assert.deepEqual(parseScalarQueryParams('?term=first&term=second'), {
    term: 'second'
  });
});

test('ignores unsafe prototype keys', () => {
  assert.deepEqual(
    parseScalarQueryParams(
      '?__proto__=polluted&constructor=ctor&toString=value&term=sonarr'
    ),
    {
      term: 'sonarr'
    }
  );
  assert.equal({}.polluted, undefined);
});

test('caps parsed parameters', () => {
  const queryString = Array.from(
    { length: 1002 },
    (_, index) => `term${index}=value`
  ).join('&');
  const params = parseScalarQueryParams(queryString);

  assert.equal(Object.keys(params).length, 1000);
  assert.equal(params.term999, 'value');
  assert.equal(params.term1000, undefined);
});
