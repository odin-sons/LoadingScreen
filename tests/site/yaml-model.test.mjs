import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseSource, writeSource, bindModel, renameKey, canonicalizeKeys } from '../../site/yaml-model.mjs';

const source = `# header
defaults:
  background: '#000000' # black
  tip:
layouts:
  first: { layers: [{ image: entry }] }
  second:
    layers:
      - image: entry
when:
  general:
    - image: a.png
      text: 2024-05-01
`;

test('scalars stay strings', () => {
  const { config } = parseSource(source);
  assert.equal(config.when.general[0].text, '2024-05-01');
  assert.equal(config.defaults.tip, null);
});

test('edits keep comments, quoting and untouched values', () => {
  const { doc, config } = parseSource(source);
  config.defaults.background = '#111111';
  const output = writeSource(doc, config);
  assert.match(output, /^# header/);
  assert.match(output, /background: '#111111' # black/);
  assert.match(output, /tip:\n/);
  assert.match(output, /text: 2024-05-01/);
});

test('renames keep the key position', () => {
  const { doc, config } = parseSource(source);
  renameKey(config.layouts, 'first', 'opening');
  const output = writeSource(doc, config);
  assert.ok(output.indexOf('opening:') < output.indexOf('second:'));
  assert.ok(!output.includes('first:'));
});

test('new documents are written from scratch', () => {
  assert.equal(writeSource(null, { order: 'random', when: { general: [{ image: 'a.png' }] } }), 'order: random\nwhen:\n  general:\n    - image: a.png\n');
});

test('aliases, deep nesting and non-mapping roots are refused', () => {
  assert.match(parseSource('a: &x 1\nb: *x\n').error.message, /aliases/);
  assert.match(parseSource('a: ' + '['.repeat(40) + ']'.repeat(40)).error.message, /nested deeper/);
  assert.match(parseSource('- a\n').error.message, /top level/);
});

test('known keys get their documented spelling', () => {
  const { config } = parseSource('Defaults: { ContinuePrompt: { Size: 3 } }\nwhen: { General: [{ Image: a.png, When: { HASITEM: Wood } }] }\n');
  const renamed = canonicalizeKeys(config, ['hasItem']);
  assert.equal(renamed.length, 7);
  assert.deepEqual(config, { defaults: { continuePrompt: { size: '3' } }, when: { general: [{ image: 'a.png', when: { hasItem: 'Wood' } }] } });
});

test('duplicate keys are refused without slowing down large maps', () => {
  assert.match(parseSource('a: 1\nb: 2\na: 3\n').error.message, /duplicate key 'a'/);
  let source = '';
  for (let index = 0; source.length < 400000; index++) source += 'k' + index + ': x\n';
  const started = Date.now();
  assert.equal(parseSource(source).error, undefined);
  assert.ok(Date.now() - started < 3000);
});

test('moving list items keeps their comments and key order', () => {
  const text = `layouts:
  story:
    layers:
      # backdrop
      - image: entry
        place: { fit: cover }
      # caption
      - size: 28
        text: caption
`;
  let { doc, config } = parseSource(text);
  const layers = config.layouts.story.layers;
  [layers[0], layers[1]] = [layers[1], layers[0]];
  let output = writeSource(doc, config);
  assert.equal(output, `layouts:
  story:
    layers:
      # caption
      - size: 28
        text: caption
      # backdrop
      - image: entry
        place: { fit: cover }
`);

  doc = parseSource(output).doc;
  bindModel(doc, config);
  [layers[0], layers[1]] = [layers[1], layers[0]];
  layers.push({ color: '#000000' });
  output = writeSource(doc, config);
  assert.equal(output, text + '      - color: "#000000"\n');
});
