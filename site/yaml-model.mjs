import { Document, parseDocument, LineCounter, isAlias, isMap, isScalar, isSeq, isPair } from './vendor/yaml.mjs';

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
    if (isMap(node)) {
      const names = new Set();
      for (const pair of node.items) {
        const name = isScalar(pair.key) ? keyText(pair.key) : pair.key;
        if (names.has(name)) return { line: lineOf(pair.key), message: 'duplicate key \'' + keyText(pair.key) + '\'', invalid: true };
        names.add(name);
      }
    }
    if (isPair(node)) {
      stack.push([node.value, depth], [node.key, depth]);
    } else if (isMap(node) || isSeq(node)) {
      if (depth + 1 > MAX_NESTING_DEPTH) return { line: lineOf(node), message: 'YAML is nested deeper than ' + MAX_NESTING_DEPTH + ' levels' };
      for (let index = node.items.length - 1; index >= 0; index--) stack.push([node.items[index], depth + 1]);
    }
  }
  return null;
}

const origins = new WeakMap();

export function toModel(node) {
  if (isMap(node)) {
    const object = {};
    for (const pair of node.items) define(object, keyText(pair.key), toModel(pair.value));
    origins.set(object, node);
    return object;
  }
  if (isSeq(node)) {
    const items = node.items.map(toModel);
    origins.set(items, node);
    return items;
  }
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
    doc = parseDocument(text, { schema: 'failsafe', lineCounter, uniqueKeys: false });
  } catch (error) {
    return { error: yamlError(error, lineOf) };
  }
  if (doc.errors.length) return { error: yamlError(doc.errors[0], lineOf) };
  const structure = checkStructure(doc.contents, lineOf);
  if (structure) return { error: { line: structure.line ?? 1, message: (structure.invalid ? 'invalid YAML, config ignored: ' + structure.message : structure.message + ', config ignored') } };
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

function plain(value) {
  if (value === null || value === undefined) return null;
  if (Array.isArray(value)) return value.map(plain);
  if (typeof value === 'object') {
    const object = {};
    for (const [name, item] of Object.entries(value)) define(object, name, plain(item));
    return object;
  }
  return String(value);
}

function patchNode(doc, node, value) {
  if (value === null || value === undefined) return isNullNode(node) && node ? node : doc.createNode(null);
  if (Array.isArray(value)) {
    if (!isSeq(node)) return doc.createNode(plain(value));
    const previous = node.items;
    const claimed = new Set(value.map((item) => item !== null && typeof item === 'object' && origins.get(item))
      .filter((origin) => origin && previous.includes(origin)));
    node.items = value.map((item, index) => {
      const origin = item !== null && typeof item === 'object' ? origins.get(item) : undefined;
      const source = claimed.has(origin) ? origin : claimed.has(previous[index]) ? undefined : previous[index];
      const patched = patchNode(doc, source, item);
      if (item !== null && typeof item === 'object') origins.set(item, patched);
      return patched;
    });
    const first = previous[0];
    if (node.commentBefore && first && node.items[0] !== first && node.items.includes(first)) {
      first.commentBefore = [node.commentBefore, first.commentBefore].filter(Boolean).join('\n');
      node.commentBefore = undefined;
    }
    return node;
  }
  if (typeof value === 'object') {
    if (!isMap(node)) return doc.createNode(plain(value));
    const names = Object.keys(value);
    const known = new Set(node.items.map((pair) => keyText(pair.key)));
    const added = names.filter((name) => !known.has(name));
    const kept = [];
    for (const pair of node.items) {
      const name = keyText(pair.key);
      if (!Object.hasOwn(value, name)) {
        if (!added.length) continue;
        const renamed = added.shift();
        if (isScalar(pair.key)) pair.key.value = renamed;
        else pair.key = doc.createNode(renamed);
        pair.value = patchNode(doc, pair.value, value[renamed]);
      } else pair.value = patchNode(doc, pair.value, value[name]);
      kept.push(pair);
    }
    for (const name of added) kept.push(doc.createPair(name, plain(value[name])));
    node.items = kept;
    return node;
  }
  const text = String(value);
  if (isScalar(node) && !isNullNode(node) && String(node.value) === text) return node;
  if (isScalar(node) && !isNullNode(node)) { node.value = text; return node; }
  return doc.createNode(text);
}

export function bindModel(doc, config) {
  const walk = (node, value) => {
    if (value === null || typeof value !== 'object') return;
    if (Array.isArray(value)) {
      if (!isSeq(node)) return;
      origins.set(value, node);
      value.forEach((item, index) => walk(node.items[index], item));
      return;
    }
    if (!isMap(node)) return;
    origins.set(value, node);
    for (const pair of node.items) {
      const name = keyText(pair.key);
      if (Object.hasOwn(value, name)) walk(pair.value, value[name]);
    }
  };
  walk(doc?.contents, config);
}

export function writeSource(doc, config) {
  const target = doc ?? new Document(null, { schema: 'failsafe' });
  target.contents = patchNode(target, target.contents, config);
  return target.toString({ lineWidth: 0 });
}
