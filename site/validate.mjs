import { Scalar, isMap, isScalar, isSeq } from './vendor/yaml.mjs';
import { isNullNode } from './yaml-model.mjs';

const PLACEMENT_KEYS = ['fit', 'position', 'opacity', 'fade'];
const ANIMATED_PLACEMENT_KEYS = [...PLACEMENT_KEYS, 'animate'];
const DEFAULT_KEYS = ['place', 'background', 'tint', 'vignette', 'mask', 'animation', 'tip', 'continueprompt', 'separator', 'spinner', 'zoom'];
const LAYER_KEYS = ['id', 'color', 'image', 'text', 'spinner', 'progress', 'size', 'align', 'background', 'place', 'animation'];
const LAYOUT_KEYS = ['layers', 'sound', 'showsleepui', 'vignette', 'mask', 'place'];
const ENTRY_KEYS = ['image', 'sound', 'voice', 'text', 'layout', 'place', 'when'];
const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg'];
const AUDIO_EXTENSIONS = ['.ogg', '.wav'];
const SIZED_SECTIONS = ['tip', 'continuePrompt'];
const INT_RANGE = [-2147483648, 2147483647];
export const MAX_PROBLEMS = 200;

const isNull = (node) => node === undefined || isNullNode(node);
const scalar = (node) => isNull(node) || !isScalar(node) ? null : String(node.value ?? '');
const name = (node) => isScalar(node) ? String(node.value ?? '').trim() : '';
const key = (node) => name(node).toLowerCase();
const same = (a, b) => a !== null && a !== undefined && a.toLowerCase() === b.toLowerCase();
const blank = (value) => value === null || value === undefined || !value.trim();
const text = (value) => value ?? '';

function valueOf(pair) {
  if (pair.value !== null && pair.value !== undefined) return pair.value;
  const empty = new Scalar('');
  empty.type = 'PLAIN';
  if (pair.key?.range) empty.range = [pair.key.range[1], pair.key.range[1], pair.key.range[1]];
  return empty;
}

function child(mapping, wanted) {
  const pair = mapping.items.find((item) => key(item.key) === wanted);
  return pair ? valueOf(pair) : undefined;
}

export function parseNumber(value) {
  if (value === null || value === undefined) return undefined;
  const trimmed = value.trim();
  if (/^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i.test(trimmed)) return Math.fround(Number(trimmed));
  const lower = trimmed.toLowerCase();
  if (['infinity', '+infinity', '∞', '+∞'].includes(lower)) return Infinity;
  if (['-infinity', '-∞'].includes(lower)) return -Infinity;
  if (['nan', '+nan', '-nan'].includes(lower)) return NaN;
  return undefined;
}

function parseInteger(value) {
  if (value === null || value === undefined || !/^[+-]?\d+$/.test(value.trim())) return undefined;
  const number = Number(value.trim());
  return number >= INT_RANGE[0] && number <= INT_RANGE[1] ? number : undefined;
}

function parseBoolean(value) {
  const lower = value?.trim().toLowerCase();
  return lower === 'true' ? true : lower === 'false' ? false : undefined;
}

function parseEnum(value, members) {
  if (value === null || value === undefined) return undefined;
  const trimmed = value.trim();
  if (!trimmed) return undefined;
  let result = 0;
  if (/^[+-]?\d/.test(trimmed)) {
    result = parseInteger(trimmed);
    if (result === undefined) return undefined;
  } else {
    for (const part of trimmed.split(',')) {
      const match = Object.keys(members).find((member) => same(member, part.trim()));
      if (match === undefined) return undefined;
      result |= members[match];
    }
  }
  return Object.values(members).includes(result) ? result : undefined;
}

export const isColor = (value) => !blank(value) && /^[0-9a-f]{6}(?:[0-9a-f]{2})?$/i.test(value.trim().replace(/^#+/, ''));

function parseLength(value) {
  if (blank(value)) return undefined;
  let number = value.trim();
  if (number.endsWith('%')) number = number.slice(0, -1);
  else if (number.toLowerCase().endsWith('px')) number = number.slice(0, -2);
  const amount = parseNumber(number);
  return amount === undefined || !Number.isFinite(amount) ? undefined : amount;
}

const normalizePath = (path) => path.trim().replace(/\\/g, '/');
const safeRelative = (normalized) =>
  !normalized.startsWith('/') && !normalized.split('/').some((segment) => segment === '..' || segment.includes(':'));

function extension(path) {
  const dot = path.lastIndexOf('.');
  const separator = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'), path.lastIndexOf(':'));
  return dot > separator && dot < path.length - 1 ? path.slice(dot) : '';
}

export function validate(parsed, options) {
  if (parsed.error) return [parsed.error];
  const { doc, lineOf } = parsed;
  const enums = options.enums;
  const problems = [];
  const layouts = new Map();
  const emptyList = (listName) => ({ name: listName, layout: null, entries: [] });
  const lists = { general: emptyList('general'), sleep: emptyList('sleep'), biome: new Map(), death: new Map(), enemy: new Map() };

  const warn = (node, message) => problems.push({ line: node ? lineOf(node) ?? 0 : 0, message });

  function unknownKeys(mapping, allowed, where) {
    for (const pair of mapping.items) if (!allowed.includes(key(pair.key))) warn(pair.key, where + ": unknown key '" + name(pair.key) + "'");
  }

  function color(node, where, label) {
    if (!isNull(node) && !isColor(scalar(node))) warn(node, where + ': invalid ' + label + " '" + text(scalar(node)) + "', use #RRGGBB or #RRGGBBAA");
  }

  function checkImage(node, path, where) {
    if (blank(path)) { warn(node, where + ': empty image path, ignored'); return false; }
    const normalized = normalizePath(path);
    if (!safeRelative(normalized)) {
      warn(node, where + ": image path '" + path + "' must be relative to the config folder, ignored");
      return false;
    }
    if (!IMAGE_EXTENSIONS.some((known) => same(known, extension(normalized)))) {
      warn(node, where + ": unsupported image type '" + path + "', use " + IMAGE_EXTENSIONS.join(', '));
      return false;
    }
    return true;
  }

  function audio(node, where, label) {
    if (isNull(node)) return;
    const path = scalar(node);
    if (blank(path)) { warn(node, where + ": '" + label + "' needs an audio path, ignored"); return; }
    const normalized = normalizePath(path);
    if (!safeRelative(normalized))
      warn(node, where + ": audio path '" + path + "' must be relative to the config folder, ignored");
    else if (!AUDIO_EXTENSIONS.some((known) => same(known, extension(normalized))))
      warn(node, where + ": unsupported audio type '" + path + "', use .ogg or .wav");
  }

  function order(node, where) {
    const value = scalar(node);
    if (!same(value, 'random') && !same(value, 'sequential')) warn(node, where + ": use 'random' or 'sequential', got '" + text(value) + "'");
  }

  function anchor(node, where) {
    if (isNull(node)) return;
    const value = scalar(node);
    if (parseEnum(value?.replace(/[-_ ]/g, ''), enums.anchor) === undefined)
      warn(node, where + ": invalid anchor '" + text(value) + "', use center, left, right, top, bottom or e.g. top-left");
  }

  function length(node, where, label, allowNegative) {
    if (isNull(node)) return null;
    const value = parseLength(scalar(node));
    if (value === undefined) { warn(node, where + ': invalid ' + label + " '" + text(scalar(node)) + "', use a number with % or px"); return null; }
    if (!allowNegative && value < 0) { warn(node, where + ': ' + label + ' must not be negative'); return null; }
    return value;
  }

  function fit(node, where) {
    if (isNull(node)) return null;
    if (isMap(node)) {
      unknownKeys(node, ['width', 'height'], where + ": 'fit'");
      const width = length(child(node, 'width'), where, 'width', false);
      const height = length(child(node, 'height'), where, 'height', false);
      if (width === null && height === null) { warn(node, where + ": 'fit' needs 'width' and/or 'height'"); return null; }
      return 'box';
    }
    const value = scalar(node);
    const mode = parseEnum(value, enums.fitMode);
    if (mode !== undefined && mode !== enums.fitMode.Box) return 'mode';
    warn(node, where + ": invalid fit '" + text(value) + "', use cover, contain, stretch, none or { width, height }");
    return null;
  }

  function position(node, where) {
    if (isNull(node)) return;
    if (isScalar(node)) { anchor(node, where); return; }
    if (!isMap(node)) { warn(node, where + ": 'position' must be an anchor name or a mapping"); return; }
    unknownKeys(node, ['anchor', 'x', 'y'], where + ": 'position'");
    anchor(child(node, 'anchor'), where);
    length(child(node, 'x'), where, 'x', true);
    length(child(node, 'y'), where, 'y', true);
  }

  function opacity(node, where) {
    if (isNull(node)) return;
    const value = parseNumber(scalar(node));
    if (value === undefined || value < 0 || value > 1) warn(node, where + ": 'opacity' must be a number from 0 to 1");
  }

  function sides(node, where) {
    let result = 0;
    for (const item of isSeq(node) ? node.items : isNull(node) ? [] : [node]) {
      const value = scalar(item);
      const side = parseEnum(value, enums.sides);
      if (side !== undefined && side !== 0) result |= side;
      else warn(item, where + ": invalid side '" + text(value) + "'");
    }
    return result;
  }

  function fade(node, where) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, where + ": 'fade' must be a mapping"); return; }
    const fadeWhere = where + ": 'fade'";
    unknownKeys(node, ['sides', 'color', 'size', 'curve'], fadeWhere);
    if (sides(child(node, 'sides'), fadeWhere) === 0) { warn(node, fadeWhere + " needs 'sides' (left, right, top, bottom or all), ignored"); return; }
    color(child(node, 'color'), fadeWhere, 'color');
    length(child(node, 'size'), fadeWhere, 'size', false);
    const curve = child(node, 'curve');
    if (!isNull(curve) && parseEnum(scalar(curve), enums.fadeCurve) === undefined)
      warn(curve, fadeWhere + ": invalid curve '" + text(scalar(curve)) + "', use linear or smooth");
  }

  function placement(node, where, keys = PLACEMENT_KEYS) {
    if (isNull(node)) return null;
    if (!isMap(node)) { warn(node, where + ': expected a mapping'); return null; }
    unknownKeys(node, keys, where);
    const fitMode = fit(child(node, 'fit'), where);
    position(child(node, 'position'), where);
    opacity(child(node, 'opacity'), where);
    fade(child(node, 'fade'), where);
    return fitMode;
  }

  function bool(node, where, label) {
    if (isNull(node)) return null;
    const value = parseBoolean(scalar(node));
    if (value === undefined) { warn(node, where + ": '" + label + "' must be true or false"); return null; }
    return value;
  }

  function legacyAnimate(place, where) {
    return isMap(place) && bool(child(place, 'animate'), where, 'animate') !== null;
  }

  function legacyZoom(node) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, 'defaults.zoom: expected a mapping'); return; }
    unknownKeys(node, ['enabled', 'amount', 'seconds'], 'defaults.zoom');
    if (bool(child(node, 'enabled'), 'defaults.zoom', 'enabled') === false) return;
    const seconds = child(node, 'seconds');
    if (isNull(seconds)) return;
    const value = parseNumber(scalar(seconds));
    if (!(Number.isFinite(value) && value > 0)) warn(seconds, "defaults.zoom: 'seconds' must be positive");
  }

  function animation(node, where) {
    if (isNull(node)) return false;
    if (isScalar(node)) {
      if (!blank(scalar(node))) return true;
      warn(node, where + ': preset name cannot be empty');
      return false;
    }
    if (!isMap(node)) { warn(node, where + ': expected a preset name or phase mapping'); return false; }
    unknownKeys(node, ['enter', 'idle', 'exit', 'speed', 'origin'], where);
    for (const phaseKey of ['enter', 'idle', 'exit']) {
      const phase = child(node, phaseKey);
      if (!isNull(phase) && blank(scalar(phase))) warn(phase, where + ": '" + phaseKey + "' must be a preset name");
    }
    anchor(child(node, 'origin'), where + ": 'origin'");
    const speed = child(node, 'speed');
    if (!isNull(speed)) {
      const value = parseNumber(scalar(speed));
      if (!(Number.isFinite(value) && value > 0)) warn(speed, where + ': speed must be positive');
    }
    return true;
  }

  function effectOpacity(node, where, label) {
    if (isNull(node)) return;
    const value = parseNumber(scalar(node));
    if (!(value !== undefined && !Number.isNaN(value) && value >= 0 && value <= 1)) warn(node, where + ": '" + label + "' must be a number from 0 to 1");
  }

  function vignette(node, where) {
    if (node === undefined) return;
    if (!isMap(node)) { warn(node, where + ': expected a mapping; effect ignored'); return; }
    unknownKeys(node, ['color', 'size', 'strength'], where);
    color(child(node, 'color'), where, 'color');
    const size = child(node, 'size');
    if (!isNull(size)) {
      const value = scalar(size);
      const amount = value?.endsWith('%') ? parseLength(value) : undefined;
      if (!(amount !== undefined && amount >= 0 && amount <= 100)) warn(size, where + ": 'size' must be from 0% to 100%");
    }
    effectOpacity(child(node, 'strength'), where, 'strength');
  }

  function mask(node, where) {
    if (node === undefined) return;
    if (!isMap(node)) { warn(node, where + ': expected a mapping; effect ignored'); return; }
    unknownKeys(node, ['color', 'opacity'], where);
    color(child(node, 'color'), where, 'color');
    effectOpacity(child(node, 'opacity'), where, 'opacity');
  }

  function textLayer(mapping, textNode, where) {
    const source = scalar(textNode);
    if (!same(source, 'caption') && !same(source, 'prompt')) { warn(textNode, where + ": 'text' needs caption or prompt, layer ignored"); return false; }
    const size = child(mapping, 'size');
    if (!isNull(size)) {
      const value = parseNumber(scalar(size));
      if (!(value !== undefined && value > 0)) warn(size, where + ": 'size' must be a positive font size");
    }
    const textColor = child(mapping, 'color');
    const background = child(mapping, 'background');
    if (!same(scalar(textColor), 'auto')) color(textColor, where, 'color');
    if (!same(scalar(background), 'auto')) color(background, where, 'background');
    const align = child(mapping, 'align');
    if (!isNull(align) && parseEnum(scalar(align), enums.textAlign) === undefined)
      warn(align, where + ": invalid align '" + text(scalar(align)) + "', use left, center or right");
    return true;
  }

  function layer(node, layoutName, index) {
    const id = isMap(node) ? scalar(child(node, 'id')) : null;
    const where = "layout '" + layoutName + "' layer '" + (id ?? index) + "'";
    if (!isMap(node)) { warn(node, where + ': a layer must be a mapping, ignored'); return null; }
    unknownKeys(node, LAYER_KEYS, where);
    const place = child(node, 'place');
    const fitMode = placement(place, where + ": 'place'", ANIMATED_PLACEMENT_KEYS);
    if (!animation(child(node, 'animation'), where + '.animation')) legacyAnimate(place, where + ": 'place'");
    const colorNode = child(node, 'color');
    const image = child(node, 'image');
    const textNode = child(node, 'text');
    const spinner = child(node, 'spinner');
    const progress = child(node, 'progress');
    if ([image, textNode, spinner, progress].filter((item) => item !== undefined).length > 1) {
      warn(node, where + ": use only one of 'image', 'text', 'spinner' and 'progress' in a layer, ignored");
      return null;
    }
    if (progress !== undefined) {
      if (parseEnum(scalar(progress), enums.progressStyle) === undefined) { warn(progress, where + ": 'progress' needs bar, ring or percent; layer ignored"); return null; }
      color(colorNode, where, 'color');
      const size = child(node, 'size');
      if (!isNull(size)) {
        const value = parseNumber(scalar(size));
        if (!(Number.isFinite(value) && value > 0)) warn(size, where + ": 'size' must be positive");
      }
      return { fromEntry: false };
    }
    if (spinner !== undefined) {
      if (colorNode !== undefined) { warn(node, where + ": use only one of 'color' and 'spinner' in a layer, ignored"); return null; }
      if (!isScalar(spinner) || blank(scalar(spinner))) { warn(spinner, where + ": 'spinner' needs hammer, ring, dots or an image path; layer ignored"); return null; }
      const style = scalar(spinner).trim();
      if (!['hammer', 'ring', 'dots'].some((known) => same(style, known)) && !checkImage(spinner, style, where)) return null;
      return { fromEntry: false };
    }
    if (textNode !== undefined) {
      if (!textLayer(node, textNode, where)) return null;
      if (fitMode === 'mode') warn(isMap(place) ? child(place, 'fit') : null, where + ": a text layer supports only 'fit: { width }' (the text width)");
      return { fromEntry: false };
    }
    for (const extra of ['size', 'align', 'background'])
      if (child(node, extra) !== undefined) warn(child(node, extra), where + ": '" + extra + "' applies only to text layers");
    if (image === undefined) {
      if (colorNode === undefined) { warn(node, where + ": a layer needs 'image', 'text' or 'color', ignored"); return null; }
      if (!isColor(scalar(colorNode))) { warn(colorNode, where + ": invalid color '" + text(scalar(colorNode)) + "', use #RRGGBB or #RRGGBBAA; layer ignored"); return null; }
      return { fromEntry: false };
    }
    if (isScalar(image) && same(scalar(image), 'separator')) {
      if (colorNode !== undefined && !isColor(scalar(colorNode)))
        warn(colorNode, where + ": invalid color '" + text(scalar(colorNode)) + "', use #RRGGBB or #RRGGBBAA; tint ignored");
      return { fromEntry: false };
    }
    if (colorNode !== undefined) warn(colorNode, where + ": 'color' is ignored on an image layer");
    if (isScalar(image) && same(scalar(image), 'entry')) return { fromEntry: true };
    let usable = 0;
    for (const item of isSeq(image) ? image.items : [image]) if (checkImage(item, scalar(item), where)) usable++;
    if (!usable) { warn(node, where + ': a layer has no usable images, ignored'); return null; }
    return { fromEntry: false };
  }

  function layout(layoutName, node) {
    let layersNode = node;
    if (isMap(node)) {
      const where = "layout '" + layoutName + "'";
      unknownKeys(node, LAYOUT_KEYS, where);
      placement(child(node, 'place'), where + ": 'place'");
      layersNode = child(node, 'layers');
      vignette(child(node, 'vignette'), where + '.vignette');
      mask(child(node, 'mask'), where + '.mask');
      audio(child(node, 'sound'), where, 'sound');
      const showSleepUi = child(node, 'showsleepui');
      if (!isNull(showSleepUi) && parseBoolean(scalar(showSleepUi)) === undefined)
        warn(showSleepUi, where + ": 'showSleepUi' must be true or false");
    }
    if (!isSeq(layersNode)) { warn(node, "layout '" + layoutName + "' needs a 'layers' list, ignored"); return null; }
    const layers = layersNode.items.map((item, index) => layer(item, layoutName, index)).filter(Boolean);
    if (!layers.length) { warn(node, "layout '" + layoutName + "' has no valid layers, ignored"); return null; }
    return { name: layoutName, showsEntry: layers.some((item) => item.fromEntry) };
  }

  function parseLayouts(node) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, "'layouts' must be a mapping of layout names"); return; }
    for (const pair of node.items) {
      const layoutName = name(pair.key);
      if (layouts.has(layoutName.toLowerCase())) { warn(pair.key, "duplicate layout '" + layoutName + "', ignored"); continue; }
      const result = layout(layoutName, valueOf(pair));
      if (result) layouts.set(layoutName.toLowerCase(), result);
    }
  }

  function conditions(node, where) {
    if (!isMap(node)) { warn(node, where + ': expected a mapping, entry ignored'); return false; }
    const kinds = enums.conditionKind;
    for (const pair of node.items) {
      const conditionKey = name(pair.key);
      const negated = conditionKey.toLowerCase().startsWith('not');
      if (parseEnum(conditionKey, kinds) === undefined && !(negated && parseEnum(conditionKey.slice(3), kinds) !== undefined)) {
        warn(pair.key, where + ": unknown condition '" + conditionKey + "', entry ignored");
        return false;
      }
      const value = valueOf(pair);
      for (const item of isSeq(value) ? value.items : [value]) {
        if (blank(scalar(item))) { warn(item, where + ": '" + conditionKey + "' needs a name, entry ignored"); return false; }
      }
    }
    return true;
  }

  function languageCode(value) {
    if (blank(value)) return null;
    const trimmed = value.trim();
    const byName = Object.keys(options.languages).find((language) => same(language, trimmed));
    if (byName) return options.languages[byName];
    return Object.values(options.languages).find((code) => same(code, trimmed)) ?? null;
  }

  function entry(node, where) {
    if (isScalar(node)) return checkImage(node, scalar(node), where) ? { layout: null } : null;
    if (!isMap(node)) { warn(node, where + ': an entry must be an image path or a mapping, ignored'); return null; }
    unknownKeys(node, ENTRY_KEYS, where);
    const image = child(node, 'image');
    const textNode = child(node, 'text');
    let translations = 0;
    if (isMap(textNode)) {
      for (const pair of textNode.items) {
        const code = languageCode(name(pair.key));
        if (!code) warn(pair.key, where + ": unknown text language '" + name(pair.key) + "'");
        else if (blank(scalar(valueOf(pair)))) warn(valueOf(pair), where + ": text for '" + code + "' must be a non-empty string");
        else translations++;
      }
    } else if (!isNull(textNode) && !isScalar(textNode)) warn(textNode, where + ": 'text' must be a string or a language mapping");
    const caption = scalar(textNode);
    if (isNull(image) && !caption && translations === 0) { warn(node, where + ": an entry needs an 'image' or a 'text', ignored"); return null; }
    if (!isNull(image) && !checkImage(image, scalar(image), where)) return null;
    audio(child(node, 'sound'), where, 'sound');
    audio(child(node, 'voice'), where, 'voice');
    const layoutName = scalar(child(node, 'layout'));
    placement(child(node, 'place'), where + ": 'place'");
    const when = child(node, 'when');
    if (!isNull(when) && !conditions(when, where + ": 'when'")) return null;
    return { layout: layoutName };
  }

  function entryList(listName, node) {
    const list = emptyList(listName);
    let entriesNode = node;
    if (isMap(node)) {
      unknownKeys(node, ['layout', 'sound', 'order', 'entries'], listName);
      list.layout = scalar(child(node, 'layout'));
      audio(child(node, 'sound'), listName, 'sound');
      const orderNode = child(node, 'order');
      if (orderNode !== undefined) order(orderNode, "'" + listName + "' order");
      entriesNode = child(node, 'entries');
    }
    if (isNull(entriesNode)) return list;
    if (!isSeq(entriesNode)) { warn(entriesNode, "'" + listName + "' entries must be a list"); return list; }
    entriesNode.items.forEach((item, index) => {
      const result = entry(item, listName + '.entries[' + index + ']');
      if (result) list.entries.push(result);
    });
    return list;
  }

  function namedLists(node, situation, target, knownNames) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, "'" + situation + "' must be a mapping of names to entries"); return; }
    for (const pair of node.items) {
      let listName = name(pair.key);
      if (situation === 'biome') listName = options.biomes.find((candidate) => same(candidate, listName)) ?? listName;
      else if (knownNames) {
        const known = knownNames.find((candidate) => same(candidate, listName));
        if (known === undefined) { warn(pair.key, 'unknown ' + situation + " name '" + listName + "', known: " + knownNames.join(', ')); continue; }
        listName = known;
      }
      if (target.has(listName.toLowerCase())) { warn(pair.key, 'duplicate ' + situation + " name '" + listName + "', ignored"); continue; }
      target.set(listName.toLowerCase(), entryList(situation + '.' + listName, valueOf(pair)));
    }
  }

  function parseWhen(node) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, "'when' must be a mapping"); return; }
    for (const pair of node.items) {
      const value = valueOf(pair);
      switch (key(pair.key)) {
        case 'general': lists.general = entryList('general', value); break;
        case 'sleep': lists.sleep = entryList('sleep', value); break;
        case 'biome': namedLists(value, 'biome', lists.biome, null); break;
        case 'death': namedLists(value, 'death', lists.death, options.deathCauses); break;
        case 'enemy': namedLists(value, 'enemy', lists.enemy, null); break;
        default: warn(pair.key, "unknown situation '" + name(pair.key) + "', use 'general', 'sleep', 'biome', 'death' or 'enemy'");
      }
    }
  }

  function defaultSection(node, section) {
    if (node === undefined) return;
    if (!isMap(node)) { warn(node, 'defaults.' + section + ': expected a mapping; built-in values used'); return; }
    const properties = options.defaultSections[section];
    for (const pair of node.items) {
      const propertyKey = name(pair.key);
      const property = Object.keys(properties).find((candidate) => same(candidate, propertyKey));
      if (property === undefined) { warn(pair.key, 'defaults.' + section + ": unknown key '" + propertyKey + "'"); continue; }
      const where = section + '.' + propertyKey;
      const value = valueOf(pair);
      const raw = scalar(value);
      const type = properties[property];
      if (type === 'bool') {
        if (parseBoolean(raw) === undefined) warn(value, where + ': must be true or false; built-in value used');
      } else if (type === 'color') {
        if (!isColor(raw)) warn(value, where + ': invalid color, use #RRGGBB or #RRGGBBAA; built-in value used');
      } else if (type === 'int') {
        const [min, max] = SIZED_SECTIONS.includes(section) && property === 'Size' ? [0, 64] : INT_RANGE;
        const number = parseInteger(raw);
        if (number === undefined || number < min || number > max) warn(value, where + ': must be an integer from ' + min + ' to ' + max + '; built-in value used');
      } else if (!Number.isFinite(parseNumber(raw))) warn(value, where + ': invalid number; built-in value used');
    }
  }

  function defaults(node) {
    if (isNull(node)) return;
    if (!isMap(node)) { warn(node, 'defaults: expected a mapping'); return; }
    unknownKeys(node, DEFAULT_KEYS, 'defaults');
    placement(child(node, 'place'), 'defaults.place', ANIMATED_PLACEMENT_KEYS);
    for (const colorKey of ['background', 'tint']) {
      const value = child(node, colorKey);
      if (value !== undefined && !isColor(scalar(value))) warn(value, 'defaults.' + colorKey + ': invalid color, use #RRGGBB or #RRGGBBAA; built-in value used');
    }
    vignette(child(node, 'vignette'), 'defaults.vignette');
    mask(child(node, 'mask'), 'defaults.mask');
    if (!animation(child(node, 'animation'), 'defaults.animation') && !legacyAnimate(child(node, 'place'), 'defaults.place'))
      legacyZoom(child(node, 'zoom'));
    defaultSection(child(node, 'tip'), 'tip');
    defaultSection(child(node, 'continueprompt'), 'continuePrompt');
    defaultSection(child(node, 'separator'), 'separator');
    defaultSection(child(node, 'spinner'), 'spinner');
  }

  function layoutReference(layoutName, where) {
    if (layoutName !== null && !layouts.has(layoutName.toLowerCase())) warn(null, where + ": unknown layout '" + layoutName + "', the default layout is used");
  }

  const root = doc.contents;
  if (!isMap(root)) return problems;
  for (const pair of root.items) {
    const value = valueOf(pair);
    switch (key(pair.key)) {
      case 'defaults': defaults(value); break;
      case 'layouts': parseLayouts(value); break;
      case 'order': order(value, 'order'); break;
      case 'when': parseWhen(value); break;
      default: warn(pair.key, "unknown top-level key '" + name(pair.key) + "'");
    }
  }
  for (const list of [lists.general, lists.sleep, ...lists.biome.values(), ...lists.death.values(), ...lists.enemy.values()]) {
    layoutReference(list.layout, list.name);
    for (const item of list.entries) layoutReference(item.layout, list.name);
  }
  for (const item of layouts.values())
    if (!item.showsEntry) warn(null, "layout '" + item.name + "' has no 'image: entry' layer, so entry images are not shown with it");
  if (problems.length > MAX_PROBLEMS) {
    const hidden = problems.length - MAX_PROBLEMS;
    problems.length = MAX_PROBLEMS;
    problems.push({ line: 0, message: hidden + ' more problems not shown' });
  }
  return problems;
}
