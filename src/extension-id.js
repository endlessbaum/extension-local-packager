import { createHash } from 'node:crypto';

export function extensionIdFromKey(key) {
  if (typeof key !== 'string' || key.trim() === '') {
    throw new Error('dist/manifest.json must contain a non-empty "key" to keep the Extension ID stable.');
  }
  let der;
  try {
    der = Buffer.from(key.replace(/\s+/g, ''), 'base64');
  } catch {
    throw new Error('manifest.json "key" is not valid Base64.');
  }
  if (der.length === 0) throw new Error('manifest.json "key" is not valid Base64.');
  const hex = createHash('sha256').update(der).digest('hex').slice(0, 32);
  return [...hex].map((character) => String.fromCharCode(97 + Number.parseInt(character, 16))).join('');
}
