import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { parseSource } from '../../site/yaml-model.mjs';
import { validate } from '../../site/validate.mjs';

const fixtures = new URL('../fixtures/config/', import.meta.url);
const options = JSON.parse(readFileSync(new URL('../../site/data/options.json', import.meta.url), 'utf8'));

for (const file of readdirSync(fixtures).filter((name) => name.endsWith('.yaml')).sort()) {
  test('validate matches the mod parser on ' + file, () => {
    const source = readFileSync(new URL(file, fixtures), 'utf8');
    const expected = readFileSync(new URL(file.replace(/\.yaml$/, '.expected'), fixtures), 'utf8')
      .replace(/\r\n/g, '\n').split('\n').filter(Boolean).map((line) => {
        const [lineNumber, ...message] = line.split('\t');
        return { line: Number(lineNumber), message: message.join('\t') };
      });
    const actual = validate(parseSource(source), options);
    assert.deepEqual(actual.map((problem) => problem.message), expected.map((problem) => problem.message));
    expected.forEach((problem, index) => {
      if (problem.line) assert.equal(actual[index].line, problem.line, problem.message);
    });
  });
}
