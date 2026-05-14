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
const nodeModulesPath = path.resolve(__dirname, '../../node_modules');

fs.symlinkSync(nodeModulesPath, path.join(outputDirectory, 'node_modules'), 'dir');

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

test('promotes scalar values before appending array-style duplicates', () => {
  assert.deepEqual(parseQueryParams('tag=alpha&tag[]=beta'), {
    tag: ['alpha', 'beta']
  });
});

test('treats indexes above the qs array limit as object keys', () => {
  assert.deepEqual(parseQueryParams('tags[21]=alpha&tags[999999999]=beta'), {
    tags: {
      21: 'alpha',
      999999999: 'beta'
    }
  });
});

test('preserves mixed array and object notation', () => {
  assert.deepEqual(parseQueryParams('tag[0]=alpha&tag[name]=beta'), {
    tag: {
      0: 'alpha',
      name: 'beta'
    }
  });
});

test('preserves scalar values before object-style duplicates', () => {
  assert.deepEqual(parseQueryParams('tag=alpha&tag[name]=beta'), {
    tag: [
      'alpha',
      {
        name: 'beta'
      }
    ]
  });
});

test('ignores Object prototype shadowing keys', () => {
  assert.deepEqual(
    parseQueryParams('tag[hasOwnProperty]=alpha&toString=beta'),
    {}
  );
});

test('caps parsed parameters at the qs default limit', () => {
  const queryString = Array.from(
    { length: 1002 },
    (_, index) => `tag${index}=value`
  ).join('&');
  const params = parseQueryParams(queryString);

  assert.equal(Object.keys(params).length, 1000);
  assert.equal(params.tag999, 'value');
  assert.equal(params.tag1000, undefined);
});

test('keeps nesting past the qs depth limit as a literal key', () => {
  assert.deepEqual(parseQueryParams('a[b][c][d][e][f][g]=h'), {
    a: {
      b: {
        c: {
          d: {
            e: {
              f: {
                '[g]': 'h'
              }
            }
          }
        }
      }
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
