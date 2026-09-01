import fs from 'node:fs/promises';
import path from 'node:path';
import { extensionIdFromKey } from './extension-id.js';

export const DEFAULT_CONFIG = 'extension-packager.json';

export async function loadProject(configFile = DEFAULT_CONFIG) {
  const absoluteConfig = path.resolve(configFile);
  const root = path.dirname(absoluteConfig);
  const config = JSON.parse(await fs.readFile(absoluteConfig, 'utf8'));
  validateConfig(config);

  const manifestPath = path.resolve(root, config.extension?.manifest ?? 'dist/manifest.json');
  const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
  if (!manifest.version) throw new Error(`${manifestPath} does not contain "version".`);
  const extensionId = extensionIdFromKey(manifest.key);

  return {
    config,
    root,
    manifest,
    manifestPath,
    extensionDir: path.dirname(manifestPath),
    extensionId,
    outputDir: path.resolve(root, config.output ?? 'release'),
    nativeHostName: config.nativeHostName ?? `com.${config.publisherId}.${config.appId.replaceAll('-', '_')}`
  };
}

export function validateConfig(config) {
  for (const field of ['appId', 'displayName', 'publisherId']) {
    if (typeof config[field] !== 'string' || !config[field].trim()) {
      throw new Error(`Configuration field "${field}" is required.`);
    }
  }
  if (!/^[a-z0-9][a-z0-9-]*$/.test(config.appId)) {
    throw new Error('"appId" must contain only lowercase letters, numbers, and hyphens.');
  }
  if (!/^[a-z0-9][a-z0-9_.-]*$/.test(config.publisherId)) {
    throw new Error('"publisherId" must be a lowercase identifier.');
  }
  try {
    const url = new URL(config.update?.manifestUrl);
    if (url.protocol !== 'https:') throw new Error();
  } catch {
    throw new Error('"update.manifestUrl" must be an HTTPS URL.');
  }
}

export async function validateLatestDocument(document, source = 'latest.json') {
  if (!document || typeof document !== 'object') throw new Error(`${source} must contain a JSON object.`);
  if (typeof document.version !== 'string' || !/^\d+\.\d+\.\d+(?:\.\d+)?$/.test(document.version)) {
    throw new Error(`${source} has an invalid "version".`);
  }
  try {
    const url = new URL(document.url);
    if (url.protocol !== 'https:') throw new Error();
  } catch {
    throw new Error(`${source} has an invalid HTTPS "url".`);
  }
  if (typeof document.sha256 !== 'string' || !/^[a-fA-F0-9]{64}$/.test(document.sha256)) {
    throw new Error(`${source} must contain a 64-character SHA-256 value.`);
  }
}
