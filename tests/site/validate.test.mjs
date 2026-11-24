import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { parseSource } from '../../site/yaml-model.mjs';
import { validate } from '../../site/validate.mjs';
import { canonicalizeKeys } from '../../site/yaml-model.mjs';

const fixtures = new URL('../fixtures/config/', import.meta.url);
const schema = JSON.parse(readFileSync(new URL('../../site/data/config.schema.json', import.meta.url), 'utf8'));

test('new annotated keys flow through the schema', () => {
  const extended = structuredClone(schema);
  extended.$defs.ScreenDefaults.properties.invented = { type: 'string' };
  const parsed = parseSource('defaults: { InVeNtEd: value }\n');
  assert.deepEqual(validate(parsed, extended), []);
  canonicalizeKeys(parsed.config, extended);
  assert.equal(parsed.config.defaults.invented, 'value');
});

for (const file of readdirSync(fixtures).filter((name) => name.endsWith('.yaml')).sort()) {
  test('validate matches the mod parser on ' + file, () => {
    const source = readFileSync(new URL(file, fixtures), 'utf8');
    const expected = readFileSync(new URL(file.replace(/\.yaml$/, '.expected'), fixtures), 'utf8')
      .replace(/\r\n/g, '\n').split('\n').filter(Boolean).map((line) => {
        const [lineNumber, code, ...message] = line.split('\t');
        return { line: Number(lineNumber), code, message: message.join('\t') };
      });
    const actual = validate(parseSource(source), schema);
    assert.deepEqual(actual, expected);
  });
}
