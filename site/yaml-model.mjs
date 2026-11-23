import { parseDocument, LineCounter, isAlias, isMap, isScalar, isSeq, isPair } from './vendor/yaml.mjs';

export const MAX_SOURCE_LENGTH = 512 * 1024;
const MAX_NESTING_DEPTH = 32;

export function isNullNode(node) {
  if (node === null || node === undefined) return true;
  if (!isScalar(node) || node.type !== 'PLAIN') return false;
  const value = String(node.value ?? '');
  return value === '' || value === '~' || value === 'null';
}

export function keyText(key) {
  if (key === null || key === undefined) return '';
  return isScalar(key) ? String(key.value ?? '') : String(key);
}

export function define(object, key, value) {
  Object.defineProperty(object, key, { value, enumerable: true, writable: true, configurable: true });
  return object;
}

function checkStructure(root, lineOf) {
  const stack = [[root, 0]];
  while (stack.length) {
    const [node, depth] = stack.pop();
    if (isAlias(node)) return { line: lineOf(node), message: 'YAML aliases (*name) are not supported' };
    if (isPair(node)) {
      stack.push([node.value, depth], [node.key, depth]);
    } else if (isMap(node) || isSeq(node)) {
      if (depth + 1 > MAX_NESTING_DEPTH) return { line: lineOf(node), message: 'YAML is nested deeper than ' + MAX_NESTING_DEPTH + ' levels' };
      for (let index = node.items.length - 1; index >= 0; index--) stack.push([node.items[index], depth + 1]);
    }
  }
  return null;
}

export function toModel(node) {
  if (isMap(node)) {
    const object = {};
    for (const pair of node.items) define(object, keyText(pair.key), toModel(pair.value));
    return object;
  }
  if (isSeq(node)) return node.items.map(toModel);
  if (isNullNode(node)) return null;
  return isScalar(node) ? String(node.value ?? '') : String(node);
}

function yamlError(error, lineOf) {
  const line = error.linePos?.[0]?.line ?? lineOf(error) ?? 1;
  const message = String(error.message || error).split('\n')[0].replace(/ at line \d+, column \d+:?$/, '');
  if (/call stack|too much recursion/i.test(message)) return { line, message: 'YAML is nested deeper than ' + MAX_NESTING_DEPTH + ' levels, config ignored' };
  return { line, message: 'invalid YAML, config ignored: ' + message };
}

export function parseSource(text) {
  if (text.length > MAX_SOURCE_LENGTH)
    return { error: { line: 1, message: 'the file is larger than ' + MAX_SOURCE_LENGTH / 1024 + ' KB, the editor does not load it' } };
  const lineCounter = new LineCounter();
  const lineOf = (node) => Array.isArray(node?.range) ? lineCounter.linePos(node.range[0]).line : undefined;
  let doc;
  try {
    doc = parseDocument(text, { schema: 'failsafe', lineCounter, uniqueKeys: true });
  } catch (error) {
    return { error: yamlError(error, lineOf) };
  }
  if (doc.errors.length) return { error: yamlError(doc.errors[0], lineOf) };
  const structure = checkStructure(doc.contents, lineOf);
  if (structure) return { error: { line: structure.line ?? 1, message: structure.message + ', config ignored' } };
  const root = doc.contents;
  if (!isNullNode(root) && !isMap(root)) return { error: { line: lineOf(root) ?? 1, message: 'the top level must be a mapping, config ignored' } };
  return { doc, lineOf, config: isMap(root) ? toModel(root) : {} };
}

export function renameKey(object, from, to) {
  const entries = Object.entries(object).map(([name, value]) => [name === from ? to : name, value]);
  for (const name of Object.keys(object)) delete object[name];
  for (const [name, value] of entries) define(object, name, value);
}

const PLACE = { fit: { width: {}, height: {} }, position: { anchor: {}, x: {}, y: {} }, opacity: {}, fade: { sides: {}, color: {}, size: {}, curve: {} }, animate: {} };
const ANIMATION = { enter: {}, idle: {}, exit: {}, speed: {}, origin: {} };
const VIGNETTE = { color: {}, size: {}, strength: {} };
const MASK = { color: {}, opacity: {} };
const TEXT_SECTION = { x: {}, bottom: {}, size: {}, color: {} };
const LAYER = { id: {}, color: {}, image: {}, text: {}, spinner: {}, progress: {}, size: {}, align: {}, background: {}, place: PLACE, animation: ANIMATION };
const LAYOUT = { layers: { '[]': LAYER }, sound: {}, showSleepUi: {}, vignette: VIGNETTE, mask: MASK, place: PLACE };

function schema(conditions) {
  const when = Object.fromEntries(conditions.map((condition) => [condition, {}]));
  const entry = { image: {}, sound: {}, voice: {}, text: {}, layout: {}, place: PLACE, when };
  const list = { layout: {}, sound: {}, order: {}, entries: { '[]': entry }, '[]': entry };
  const named = { '*': list };
  return {
    defaults: {
      place: PLACE, background: {}, tint: {}, vignette: VIGNETTE, mask: MASK, animation: ANIMATION,
      tip: TEXT_SECTION, continuePrompt: TEXT_SECTION, separator: { show: {}, x: {}, bottom: {}, onLayouts: {} },
      spinner: { center: {}, x: {}, bottom: {} }, zoom: { enabled: {}, amount: {}, seconds: {} }
    },
    layouts: { '*': LAYOUT },
    order: {},
    when: { general: list, sleep: list, biome: named, death: named, enemy: named }
  };
}

export function canonicalizeKeys(config, conditions) {
  const renamed = [];
  const walk = (value, shape) => {
    if (Array.isArray(value)) {
      if (shape['[]']) value.forEach((item) => walk(item, shape['[]']));
      return;
    }
    if (value === null || typeof value !== 'object') return;
    for (const name of Object.keys(value)) {
      if (shape['*']) { walk(value[name], shape['*']); continue; }
      const canonical = Object.keys(shape).find((candidate) => candidate !== '[]' && candidate.toLowerCase() === name.trim().toLowerCase());
      if (!canonical) continue;
      if (canonical !== name && !Object.hasOwn(value, canonical)) {
        renameKey(value, name, canonical);
        renamed.push(name + ' → ' + canonical);
      }
      walk(value[Object.hasOwn(value, canonical) ? canonical : name], shape[canonical]);
    }
  };
  walk(config, schema(conditions));
  return renamed;
}
