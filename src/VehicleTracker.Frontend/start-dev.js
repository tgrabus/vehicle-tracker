#!/usr/bin/env node
'use strict';

/**
 * Dev server launcher — reads DEV_SERVER_PORT injected by .NET Aspire.
 * Spawns Angular CLI via Node directly (no shell) to avoid encoding issues
 * with non-ASCII characters in the certificate path (Windows APPDATA).
 */

const { spawn } = require('child_process');
const path = require('path');
const os = require('os');
const fs = require('fs');

const isWindows = process.platform === 'win32';

const certDir = isWindows
  ? path.join(process.env.APPDATA, 'ASP.NET', 'https')
  : path.join(os.homedir(), '.aspnet', 'https');

const pkgName = require('./package.json').name;
const port = process.env.DEV_SERVER_PORT || '4200';

const certFile = path.join(certDir, `${pkgName}.pem`);
const keyFile = path.join(certDir, `${pkgName}.key`);

// Use trusted ASP.NET Core cert if exported; angular.json ssl:true is the fallback.
const sslArgs = fs.existsSync(certFile) && fs.existsSync(keyFile)
  ? [`--ssl-cert=${certFile}`, `--ssl-key=${keyFile}`]
  : [];

const ngCli = path.join(__dirname, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');

const child = spawn(
  process.execPath,
  [ngCli, 'serve', `--port=${port}`, ...sslArgs],
  { stdio: 'inherit', shell: false }
);

child.on('exit', (code) => process.exit(code ?? 1));

