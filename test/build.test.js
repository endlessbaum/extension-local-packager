import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { build } from '../src/build.js';

test('build creates Windows bootstrap installer and uninstaller', async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'extension-packager-'));
  try {
    await fs.mkdir(path.join(root, 'dist'));
    await fs.writeFile(path.join(root, 'dist', 'manifest.json'), JSON.stringify({
      manifest_version: 3,
      name: 'Test Extension',
      version: '1.0.0',
      key: Buffer.from('test public key').toString('base64')
    }));
    await fs.writeFile(path.join(root, 'extension-packager.json'), JSON.stringify({
      appId: 'test-extension',
      displayName: 'Test Extension',
      publisherId: 'tester',
      extension: { manifest: './dist/manifest.json' },
      update: { manifestUrl: 'https://example.com/latest.json' },
      output: './release'
    }));

    await build(path.join(root, 'extension-packager.json'));
    for (const file of ['Setup.cmd', 'Setup.ps1', 'Uninstall.cmd', 'Uninstall.ps1', 'package-info.json']) {
      assert.equal((await fs.stat(path.join(root, 'release', file))).isFile(), true);
    }
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
});
