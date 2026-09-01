import test from 'node:test';
import assert from 'node:assert/strict';
import { extensionIdFromKey } from '../src/extension-id.js';
import { validateConfig, validateLatestDocument } from '../src/config.js';

test('extension ID contains 32 letters in the a-p range', () => {
  assert.match(extensionIdFromKey(Buffer.from('public key').toString('base64')), /^[a-p]{32}$/);
});

test('configuration requires HTTPS update metadata', () => {
  assert.throws(() => validateConfig({ appId: 'test', displayName: 'Test', publisherId: 'me', update: { manifestUrl: 'http://example.com/latest.json' } }), /HTTPS/);
});

test('latest document validates version, URL, and checksum', async () => {
  await validateLatestDocument({ version: '1.2.3', url: 'https://example.com/dist.zip', sha256: 'a'.repeat(64) });
  await assert.rejects(() => validateLatestDocument({ version: 'bad', url: 'https://example.com/dist.zip', sha256: 'a'.repeat(64) }), /version/);
});
