const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const testsDirectory = path.resolve(__dirname, 'tests');
const testFiles = fs
  .readdirSync(testsDirectory)
  .filter((file) => file.endsWith('.test.cjs'))
  .map((file) => path.join(testsDirectory, file));

const result = childProcess.spawnSync(
  process.execPath,
  ['--test', ...testFiles],
  {
    stdio: 'inherit'
  }
);

process.exit(result.status ?? 1);
