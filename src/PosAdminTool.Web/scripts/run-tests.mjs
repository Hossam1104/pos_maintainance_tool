import { spawnSync } from 'node:child_process';

const args = process.argv.slice(2).filter((argument) => argument !== '--run');
const result = spawnSync(process.execPath, ['./node_modules/@angular/cli/bin/ng.js', 'test', '--watch=false', ...args], { stdio: 'inherit' });
process.exit(result.status ?? 1);
