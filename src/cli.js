import fs from 'node:fs/promises';
import path from 'node:path';
import { DEFAULT_CONFIG, loadProject, validateLatestDocument } from './config.js';
import { build } from './build.js';

function option(args, name, fallback) {
  const index = args.indexOf(name);
  return index === -1 ? fallback : args[index + 1];
}

export async function run(args) {
  const command = args[0] ?? 'help';
  const configFile = option(args, '--config', DEFAULT_CONFIG);
  if (command === 'init') return init(configFile);
  if (command === 'validate') return validate(configFile, args.includes('--online'));
  if (command === 'build') return build(configFile);
  if (command === 'help' || command === '--help' || command === '-h') return printHelp();
  throw new Error(`Unknown command "${command}". Run with --help for usage.`);
}

async function init(configFile) {
  const destination = path.resolve(configFile);
  const template = {
    $schema: 'https://raw.githubusercontent.com/endlessbaum/extension-local-packager/main/schema/extension-packager.schema.json',
    appId: 'my-extension',
    displayName: 'My Extension',
    publisherId: 'my-name',
    extension: { manifest: './dist/manifest.json' },
    update: { manifestUrl: 'https://raw.githubusercontent.com/USER/REPOSITORY/main/latest.json' },
    output: './release'
  };
  await fs.writeFile(destination, `${JSON.stringify(template, null, 2)}\n`, { flag: 'wx' });
  console.log(`Created ${destination}`);
}

async function validate(configFile, online) {
  const project = await loadProject(configFile);
  if (online) {
    const response = await fetch(project.config.update.manifestUrl);
    if (!response.ok) throw new Error(`Could not download latest.json: HTTP ${response.status}`);
    await validateLatestDocument(await response.json(), project.config.update.manifestUrl);
  }
  console.log(`Valid: ${project.config.displayName}`);
  console.log(`Extension ID: ${project.extensionId}`);
  console.log(`Native host: ${project.nativeHostName}`);
}

function printHelp() {
  console.log(`extension-local-packager

Usage:
  extension-local-packager init [--config FILE]
  extension-local-packager validate [--config FILE] [--online]
  extension-local-packager build [--config FILE]
`);
}
