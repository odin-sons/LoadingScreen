import { parseSource, bindModel, canonicalizeKeys, define, renameKey, writeSource, MAX_SOURCE_LENGTH } from './yaml-model.mjs';
import { validate } from './validate.mjs';
import { drawPreview } from './preview.mjs';

const $ = (selector) => document.querySelector(selector);
const form = $('#form');
const yamlBox = $('#yaml');
const options = await loadOptions();
let config = {
  defaults: { background: '#000000', place: { fit: 'contain', position: { anchor: 'center' } } },
  layouts: { 'my-layout': { layers: [
    { color: '#172b3a' },
    { image: 'entry', place: { fit: 'cover' } },
    { text: 'caption', size: 28, color: '#FFFFFF', align: 'center', place: { fit: { width: '75%' }, position: { anchor: 'bottom', y: '-8%' } } }
  ] } },
  when: { general: { layout: 'my-layout', entries: [{ image: 'screens/my-image.jpg', text: { en: 'The journey continues.', ru: 'Путешествие продолжается.' } }] } }
};
let tab = 'defaults';
let images = new Map();
let previewName = 'my-layout';
let sourceDirty = false;
let pendingApply = 0;
let sourceDoc = null;
let focusHint = null;
let announceTimer = 0;

async function loadOptions() {
  try {
    const response = await fetch('./data/options.json');
    if (!response.ok) throw new Error('HTTP ' + response.status);
    return await response.json();
  } catch (error) {
    console.error('Editor: data/options.json could not be loaded', error);
    $('#edit-status').textContent = 'Not loaded';
    form.replaceChildren(el('p', 'section-note', 'The editor data could not be loaded (' + error.message + '). Reload the page; if you opened the file directly, serve the folder over HTTP instead.'));
    yamlBox.disabled = true;
    throw error;
  }
}

const isMapping = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const mappingOr = (value) => isMapping(value) ? value : {};
const scalarText = (value) => typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' ? String(value) : '';
const choice = scalarText;
const listText = (value, separator) => Array.isArray(value) ? value.map(scalarText).join(separator) : value;

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function append(parent, ...children) {
  children.forEach((child) => parent.append(child));
  return parent;
}

function field(parent, label, value, write, { type = 'text', values, list, wide = false, multiline = false, onChange = false, placeholder = '' } = {}) {
  const wrap = el('div', 'field' + (wide ? ' wide' : ''));
  const id = 'f-' + Math.random().toString(36).slice(2);
  const title = el('label', '', label);
  title.htmlFor = id;
  const complex = value !== null && typeof value === 'object';
  const current = scalarText(value);
  let input;
  if (multiline) input = el('textarea');
  else if (values) {
    input = el('select');
    (values.includes(current) ? values : [...values, current]).forEach((item) => {
      const option = el('option', '', item);
      option.value = item;
      input.append(option);
    });
  } else input = el('input');
  input.id = id;
  if (!values && !multiline) input.type = type;
  if (list) {
    const data = el('datalist');
    data.id = id + '-list';
    list.forEach((item) => {
      const option = el('option');
      option.value = item;
      data.append(option);
    });
    input.setAttribute('list', data.id);
    wrap.append(data);
  }
  input.placeholder = complex ? 'Edit this value in the YAML source' : placeholder;
  input.disabled = complex;
  input.value = current;
  input.addEventListener(onChange ? 'change' : 'input', () => {
    write(input.value);
    sync();
  });
  append(wrap, title, input);
  parent.append(wrap);
  return input;
}

function grid(parent) {
  const node = el('div', 'field-grid');
  parent.append(node);
  return node;
}

function group(parent, title, note) {
  const node = el('section', 'group');
  node.append(el('h3', '', title));
  if (note) node.append(el('p', 'section-note', note));
  parent.append(node);
  return node;
}

function button(parent, text, action, danger = false) {
  const node = el('button', 'mini' + (danger ? ' danger' : ''), text);
  node.type = 'button';
  node.addEventListener('click', action);
  parent.append(node);
  return node;
}

function card(parent, title, remove, move) {
  const node = el('div', 'card');
  const head = el('div', 'card-head');
  head.append(el('strong', '', title));
  const actions = el('div', 'card-actions');
  if (move) {
    button(actions, '↑', () => move(-1)).setAttribute('aria-label', 'Move ' + title + ' up');
    button(actions, '↓', () => move(1)).setAttribute('aria-label', 'Move ' + title + ' down');
  }
  labelled(button(actions, 'Remove', remove, true), 'Remove ' + title);
  append(head, actions);
  append(node, head);
  parent.append(node);
  return node;
}

function sourceOnly(parent, what) {
  parent.append(el('p', 'section-note', 'This ' + what + ' uses a form the editor cannot show. Edit it in the YAML source.'));
}

function set(target, key, value, numeric = false) {
  if (value === '') delete target[key];
  else define(target, key, numeric && Number.isFinite(Number(value)) ? Number(value) : value);
}

function mover(items, index, attach, title) {
  return (direction) => {
    const next = index + direction;
    if (next < 0 || next >= items.length) return;
    attach();
    [items[index], items[next]] = [items[next], items[index]];
    focusHint = { label: 'Move ' + title(next) + (direction < 0 ? ' up' : ' down') };
    render(); sync();
  };
}

function labelled(node, label) {
  node.setAttribute('aria-label', label);
  return node;
}

function placeEditor(parent, owner, title, attach = () => {}) {
  const box = group(parent, title);
  const place = mappingOr(owner.place);
  const keep = () => { attach(); owner.place = place; };
  const fit = place.fit;
  const fitMode = isMapping(fit) ? 'box' : choice(fit);
  const fields = grid(box);
  field(fields, 'Fit', fitMode, (v) => { keep(); if (!v) delete place.fit; else place.fit = v === 'box' ? { width: '50%' } : v; render(); }, { values: ['', 'contain', 'cover', 'stretch', 'none', 'box'] });
  if (fitMode === 'box') {
    field(fields, 'Box width', fit.width, (v) => { keep(); set(fit, 'width', v); }, { placeholder: '50%' });
    field(fields, 'Box height', fit.height, (v) => { keep(); set(fit, 'height', v); }, { placeholder: '250px' });
  }
  const position = isMapping(place.position) ? place.position : typeof place.position === 'string' ? { anchor: place.position } : {};
  const keepPosition = () => { keep(); place.position = position; };
  field(fields, 'Anchor', position.anchor, (v) => { keepPosition(); set(position, 'anchor', v); }, { list: options.anchors, placeholder: 'center' });
  field(fields, 'Offset X', position.x, (v) => { keepPosition(); set(position, 'x', v); }, { placeholder: '0px or 10%' });
  field(fields, 'Offset Y', position.y, (v) => { keepPosition(); set(position, 'y', v); }, { placeholder: '0px or -10%' });
  field(fields, 'Opacity', place.opacity, (v) => { keep(); set(place, 'opacity', v, true); }, { type: 'number', placeholder: '0–1' });
  const fade = mappingOr(place.fade);
  const keepFade = () => { keep(); place.fade = fade; };
  const fadeFields = grid(group(box, 'Edge fade'));
  field(fadeFields, 'Sides', listText(fade.sides, ', '), (v) => { keep(); if (!v) delete place.fade; else { place.fade = fade; fade.sides = v.split(',').map((s) => s.trim()).filter(Boolean); } }, { placeholder: 'bottom, left' });
  field(fadeFields, 'Fade color', fade.color, (v) => { keepFade(); set(fade, 'color', v); }, { placeholder: '#000000' });
  field(fadeFields, 'Fade size', fade.size, (v) => { keepFade(); set(fade, 'size', v); }, { placeholder: '15%' });
  field(fadeFields, 'Curve', choice(fade.curve), (v) => { keepFade(); set(fade, 'curve', v); }, { values: ['', 'smooth', 'linear'] });
}

function animationEditor(parent, owner, attach = () => {}) {
  const box = group(parent, 'Animation');
  const animation = isMapping(owner.animation) ? owner.animation : typeof owner.animation === 'string' ? { idle: owner.animation } : {};
  const keep = () => { attach(); owner.animation = animation; };
  const fields = grid(box);
  ['enter', 'idle', 'exit'].forEach((phase) => field(fields, phase[0].toUpperCase() + phase.slice(1), animation[phase], (v) => { keep(); set(animation, phase, v); }, { list: options.animations, placeholder: 'none' }));
  field(fields, 'Speed', animation.speed, (v) => { keep(); set(animation, 'speed', v, true); }, { type: 'number', placeholder: '1' });
  field(fields, 'Origin', animation.origin, (v) => { keep(); set(animation, 'origin', v); }, { list: options.anchors, placeholder: 'center' });
}

function effectsEditor(parent, owner, attach = () => {}) {
  const fields = grid(group(parent, 'Screen effects'));
  const vignette = mappingOr(owner.vignette);
  const keepVignette = () => { attach(); owner.vignette = vignette; };
  field(fields, 'Vignette color', vignette.color, (v) => { keepVignette(); set(vignette, 'color', v); }, { placeholder: '#000000' });
  field(fields, 'Vignette size', vignette.size, (v) => { keepVignette(); set(vignette, 'size', v); }, { placeholder: '25%' });
  field(fields, 'Vignette strength', vignette.strength, (v) => { keepVignette(); set(vignette, 'strength', v, true); }, { type: 'number', placeholder: '0–1' });
  const mask = mappingOr(owner.mask);
  const keepMask = () => { attach(); owner.mask = mask; };
  field(fields, 'Mask color', mask.color, (v) => { keepMask(); set(mask, 'color', v); }, { placeholder: '#000000' });
  field(fields, 'Mask opacity', mask.opacity, (v) => { keepMask(); set(mask, 'opacity', v, true); }, { type: 'number', placeholder: '0–1' });
}

function defaultsForm() {
  const intro = group(form, 'Base appearance', 'These values apply when a layout or layer does not override them.');
  const fields = grid(intro);
  const defaults = mappingOr(config.defaults);
  const attach = () => { config.defaults = defaults; };
  field(fields, 'Background', defaults.background, (v) => { attach(); set(defaults, 'background', v); }, { placeholder: '#000000' });
  field(fields, 'Image tint', defaults.tint, (v) => { attach(); set(defaults, 'tint', v); }, { placeholder: '#FFFFFF' });
  field(fields, 'Global entry order', choice(config.order), (v) => set(config, 'order', v), { values: ['', 'random', 'sequential'] });
  placeEditor(form, defaults, 'Default placement', attach);
  animationEditor(form, defaults, attach);
  effectsEditor(form, defaults, attach);
  ['tip', 'continuePrompt', 'separator', 'spinner'].forEach((name) => {
    const section = group(form, name === 'continuePrompt' ? 'Continue prompt' : name[0].toUpperCase() + name.slice(1));
    const object = mappingOr(defaults[name]);
    const keep = () => { attach(); defaults[name] = object; };
    const row = grid(section);
    ['x', 'bottom'].forEach((key) => field(row, key === 'x' ? 'Offset X (px)' : 'Bottom (px)', object[key], (v) => { keep(); set(object, key, v, true); }, { type: 'number' }));
    if (name === 'tip' || name === 'continuePrompt') {
      field(row, 'Font size', object.size, (v) => { keep(); set(object, 'size', v, true); }, { type: 'number' });
      field(row, 'Color', object.color, (v) => { keep(); set(object, 'color', v); });
    }
    const flag = name === 'separator' ? ['show', 'Show'] : name === 'spinner' ? ['center', 'Center'] : null;
    if (flag) field(row, flag[1], choice(object[flag[0]]).toLowerCase(), (v) => { keep(); set(object, flag[0], v === '' ? '' : v === 'true'); }, { values: ['', 'true', 'false'] });
  });
}

function languageName(code) {
  const lower = code.trim().toLowerCase();
  const match = Object.entries(options.languages).find(([language, value]) => value.toLowerCase() === lower || language.toLowerCase() === lower);
  return match ? match[0].replace(/_/g, ' ') + ' (' + match[1] + ')' : code;
}

function languageCode(key) {
  const name = Object.keys(options.languages).find((language) => language.toLowerCase() === key.trim().toLowerCase());
  return name ? options.languages[name] : key;
}

const LAYER_KINDS = ['image', 'text', 'spinner', 'progress', 'color'];

function layerKind(layer) {
  return LAYER_KINDS.find((name) => Object.hasOwn(layer, name)) ?? 'image';
}

function layerForm(parent, layer, layers, index) {
  const remove = () => { layers.splice(index, 1); render(); sync(); };
  const layerTitle = (position) => 'Layer ' + (position + 1) + (isMapping(layers[position]) ? ' · ' + layerKind(layers[position]) : '');
  const move = mover(layers, index, () => {}, layerTitle);
  if (!isMapping(layer)) {
    sourceOnly(card(parent, layerTitle(index), remove, move), 'layer');
    return;
  }
  const kind = layerKind(layer);
  const item = card(parent, layerTitle(index), remove, move);
  const fields = grid(item);
  field(fields, 'Kind', kind, (v) => {
    LAYER_KINDS.forEach((key) => delete layer[key]);
    delete layer.size; delete layer.align; delete layer.background;
    layer[v] = { image: 'entry', text: 'caption', spinner: 'ring', progress: 'bar', color: '#000000' }[v];
    render();
  }, { values: LAYER_KINDS });
  field(fields, 'Layer ID', layer.id, (v) => set(layer, 'id', v), { placeholder: 'optional' });
  if (kind === 'image') field(fields, 'Image path or keyword', listText(layer.image, '\n'), (v) => { layer.image = v.includes('\n') ? v.split('\n').map((s) => s.trim()).filter(Boolean) : v; }, { multiline: true, wide: true, placeholder: 'entry, separator, or images/logo.png\nOne path per line for alternatives' });
  if (kind === 'text') {
    field(fields, 'Text source', choice(layer.text), (v) => { layer.text = v; }, { values: ['caption', 'prompt'] });
    field(fields, 'Font size', layer.size, (v) => set(layer, 'size', v, true), { type: 'number' });
    field(fields, 'Text color', layer.color, (v) => set(layer, 'color', v), { placeholder: '#FFFFFF or auto' });
    field(fields, 'Box background', layer.background, (v) => set(layer, 'background', v), { placeholder: '#00000099 or auto' });
    field(fields, 'Align', choice(layer.align), (v) => set(layer, 'align', v), { values: ['', 'left', 'center', 'right'] });
  }
  if (kind === 'spinner') field(fields, 'Spinner style or image path', layer.spinner, (v) => set(layer, 'spinner', v), { list: ['hammer', 'ring', 'dots'] });
  if (kind === 'progress') {
    field(fields, 'Progress style', choice(layer.progress), (v) => set(layer, 'progress', v), { values: ['bar', 'ring', 'percent'] });
    field(fields, 'Size', layer.size, (v) => set(layer, 'size', v, true), { type: 'number' });
    field(fields, 'Color', layer.color, (v) => set(layer, 'color', v));
  }
  if (kind === 'color') field(fields, 'Fill color', layer.color, (v) => set(layer, 'color', v), { placeholder: '#000000' });
  placeEditor(item, layer, 'Placement');
  animationEditor(item, layer);
}

function layoutsForm() {
  const head = group(form, 'Layouts', 'Layers draw in order, from back to front. Use image: entry to show an entry’s image.');
  const layouts = mappingOr(config.layouts);
  button(head, '+ Add layout', () => {
    config.layouts = layouts;
    const name = uniqueName(layouts, 'new-layout');
    layouts[name] = { layers: [{ image: 'entry', place: { fit: 'cover' } }] };
    previewName = name; render(); sync();
  });
  Object.entries(layouts).forEach(([name, layout]) => {
    const item = card(form, name, () => { delete layouts[name]; render(); sync(); });
    const fields = grid(item);
    field(fields, 'Layout name', name, (v) => {
      const next = v.trim();
      if (next && next !== name && !Object.hasOwn(layouts, next)) {
        renameKey(layouts, name, next);
        if (previewName === name) previewName = next;
        render();
      }
    }, { onChange: true });
    if (!isMapping(layout)) { sourceOnly(item, 'layout'); return; }
    field(fields, 'Looping sound', layout.sound, (v) => set(layout, 'sound', v), { placeholder: 'music/forest.ogg' });
    placeEditor(item, layout, 'Layout placement');
    effectsEditor(item, layout);
    const layersBox = group(item, 'Layers');
    const layers = Array.isArray(layout.layers) ? layout.layers : [];
    labelled(button(layersBox, '+ Add layer', () => { layout.layers = layers; layers.push({ image: 'entry' }); render(); sync(); }), 'Add layer to ' + name);
    layers.forEach((layer, index) => layerForm(layersBox, layer, layers, index));
  });
}

function uniqueName(collection, base) {
  let name = base, suffix = 2;
  while (Object.hasOwn(collection, name)) name = base + '-' + suffix++;
  return name;
}

function entryForm(parent, entries, index, attachEntries) {
  const raw = entries[index];
  const entry = isMapping(raw) ? raw : typeof raw === 'string' ? { image: raw } : {};
  const keep = () => { attachEntries(); entries[index] = entry; };
  const entryTitle = (position) => 'Entry ' + (position + 1);
  const item = card(parent, entryTitle(index), () => { attachEntries(); entries.splice(index, 1); render(); sync(); }, mover(entries, index, attachEntries, entryTitle));
  if (raw !== null && raw !== undefined && typeof raw !== 'string' && !isMapping(raw)) { sourceOnly(item, 'entry'); return; }
  const fields = grid(item);
  field(fields, 'Image path', entry.image, (v) => {
    if (typeof raw === 'string' && v) { attachEntries(); entries[index] = v; return; }
    keep(); set(entry, 'image', v);
  }, { placeholder: 'screens/example.jpg' });
  field(fields, 'Layout override', entry.layout, (v) => { keep(); set(entry, 'layout', v); }, { list: Object.keys(mappingOr(config.layouts)), placeholder: 'Use list layout' });
  field(fields, 'Looping sound', entry.sound, (v) => { keep(); set(entry, 'sound', v); }, { placeholder: 'music/forest.ogg' });
  field(fields, 'Voice', entry.voice, (v) => { keep(); set(entry, 'voice', v); }, { placeholder: 'voice/welcome.wav' });
  const translated = isMapping(entry.text);
  field(fields, 'Text format', translated ? 'translations' : 'plain', (v) => {
    keep();
    entry.text = v === 'translations' ? { en: typeof entry.text === 'string' ? entry.text : '' } : (entry.text?.en || Object.values(mappingOr(entry.text)).find((value) => typeof value === 'string') || '');
    render();
  }, { values: ['plain', 'translations'] });
  if (translated) {
    const text = entry.text;
    Object.keys(text).forEach((code) => field(fields, 'Text · ' + languageName(code), text[code], (v) => { keep(); set(text, code, v); }, { multiline: true, wide: true }));
    const used = Object.keys(text).map((code) => languageCode(code).toLowerCase());
    const missing = [...new Set(Object.values(options.languages))].filter((code) => !used.includes(code.toLowerCase()));
    if (missing.length) field(fields, 'Add translation', '', (v) => { if (v) { keep(); define(text, v.split(' ')[0], ''); render(); } }, { values: ['', ...missing.map((code) => code + ' · ' + languageName(code))], onChange: true });
  } else field(fields, 'Caption text', typeof entry.text === 'string' ? entry.text : '', (v) => { keep(); set(entry, 'text', v); }, { multiline: true, wide: true });
  placeEditor(item, entry, 'Entry image placement', keep);
  const conditions = group(item, 'Conditions', 'Every condition must match. A custom item or player key can be typed.');
  const when = mappingOr(entry.when);
  const keepWhen = () => { keep(); entry.when = when; };
  labelled(button(conditions, '+ Add condition', () => {
    const key = options.conditions.find((name) => !Object.hasOwn(when, name));
    if (key) { keepWhen(); when[key] = ''; }
    render(); sync();
  }), 'Add condition to ' + entryTitle(index));
  Object.entries(when).forEach(([key, value]) => {
    const row = card(conditions, key, () => { keepWhen(); delete when[key]; render(); sync(); });
    const fields = grid(row);
    field(fields, 'Condition', key, (v) => {
      if (v && v !== key && !Object.hasOwn(when, v)) {
        keepWhen();
        renameKey(when, key, v);
        render();
      }
    }, { list: options.conditions, onChange: true });
    const suggestions = key.toLowerCase().includes('biome') ? options.biomes : key.toLowerCase().includes('global') ? options.globalKeys : [];
    field(fields, 'Name or comma-separated names', listText(value, ', '), (v) => { keepWhen(); define(when, key, v.includes(',') ? v.split(',').map((s) => s.trim()).filter(Boolean) : v); }, { list: suggestions });
  });
}

function listForm(parent, bucket, name, named = false) {
  const when = mappingOr(config.when);
  const container = named ? mappingOr(when[bucket]) : when;
  const raw = container[name];
  const list = isMapping(raw) ? raw : { entries: Array.isArray(raw) ? raw : typeof raw === 'string' ? [raw] : [] };
  const entries = Array.isArray(list.entries) ? list.entries : [];
  const attachContainer = () => { config.when = when; if (named) when[bucket] = container; };
  const keepList = () => { attachContainer(); container[name] = list; };
  const keepEntries = () => {
    if (raw === entries) { attachContainer(); return; }
    keepList();
    list.entries = entries;
  };
  const item = card(parent, named ? bucket + ' · ' + name : name, () => { attachContainer(); delete container[name]; render(); sync(); });
  const fields = grid(item);
  if (named) field(fields, 'Name', name, (v) => {
    const next = v.trim();
    if (next && next !== name && !Object.hasOwn(container, next)) { renameKey(container, name, next); render(); }
  }, { list: bucket === 'biome' ? options.biomes : bucket === 'death' ? options.deathCauses : [], onChange: true });
  if (raw !== null && raw !== undefined && typeof raw !== 'string' && !Array.isArray(raw) && !isMapping(raw)) { sourceOnly(item, 'list'); return; }
  field(fields, 'List layout', list.layout, (v) => { keepList(); set(list, 'layout', v); }, { list: Object.keys(mappingOr(config.layouts)), placeholder: 'Use default' });
  field(fields, 'Order', choice(list.order), (v) => { keepList(); set(list, 'order', v); }, { values: ['', 'random', 'sequential'] });
  field(fields, 'Looping sound', list.sound, (v) => { keepList(); set(list, 'sound', v); }, { placeholder: 'music/forest.ogg' });
  const entriesBox = group(item, 'Entries');
  labelled(button(entriesBox, '+ Add entry', () => { keepEntries(); entries.push({ image: 'screens/image.jpg' }); render(); sync(); }), 'Add entry to ' + (named ? bucket + ' ' + name : name));
  entries.forEach((entry, index) => entryForm(entriesBox, entries, index, keepEntries));
}

function whenForm() {
  const when = mappingOr(config.when);
  group(form, 'Situations', 'General covers ordinary loading. Biome covers teleport and death fallback; death covers hit and damage types.');
  ['general', 'sleep'].forEach((name) => {
    const section = group(form, name === 'general' ? 'General' : 'Sleep');
    if (Object.hasOwn(when, name)) listForm(section, name, name);
    else button(section, '+ Add ' + name + ' list', () => { config.when = when; when[name] = { entries: [] }; render(); sync(); });
  });
  ['biome', 'death', 'enemy'].forEach((bucket) => {
    const section = group(form, bucket[0].toUpperCase() + bucket.slice(1) + ' lists');
    button(section, '+ Add ' + bucket + ' list', () => {
      const names = bucket === 'biome' ? options.biomes : bucket === 'death' ? options.deathCauses : ['Troll'];
      const collection = mappingOr(when[bucket]);
      config.when = when;
      when[bucket] = collection;
      const name = names.find((candidate) => !Object.hasOwn(collection, candidate)) || uniqueName(collection, 'custom');
      collection[name] = { entries: [] }; render(); sync();
    });
    Object.keys(mappingOr(when[bucket])).forEach((name) => listForm(section, bucket, name, true));
  });
}

const SECTIONS = { defaults: defaultsForm, layouts: layoutsForm, when: whenForm };

const controls = () => [...form.querySelectorAll('input, select, textarea, button')];
const controlLabel = (node) => node.getAttribute('aria-label') || node.labels?.[0]?.textContent || node.textContent;

function restoreFocus(target) {
  const list = controls();
  if (!list.length) return;
  const index = Math.max(0, target.index);
  const distance = (position) => Math.abs(position - index);
  const matches = list.map((node, position) => [node, position]).filter(([node]) => controlLabel(node) === target.label);
  const best = matches.sort((a, b) => distance(a[1]) - distance(b[1]))[0]?.[0] ?? list[Math.min(index, list.length - 1)];
  best.focus();
}

function render() {
  const active = form.contains(document.activeElement) ? document.activeElement : null;
  const before = active ? { index: controls().indexOf(active), label: controlLabel(active) } : null;
  form.replaceChildren();
  try {
    SECTIONS[tab]();
  } catch (error) {
    console.error('Editor: the ' + tab + ' section could not be rendered', error);
    form.replaceChildren(el('p', 'section-note', 'The ' + tab + ' section could not be shown in the form (' + error.message + '). Edit it in the YAML source.'));
  }
  if (before) restoreFocus(focusHint ? { ...before, ...focusHint } : before);
  focusHint = null;
  updatePreview();
}

function selectTab(buttonNode, moveFocus = false) {
  flushSource();
  tab = buttonNode.dataset.tab;
  document.querySelectorAll('[data-tab]').forEach((node) => {
    const selected = node === buttonNode;
    node.classList.toggle('active', selected);
    node.setAttribute('aria-selected', String(selected));
    node.tabIndex = selected ? 0 : -1;
  });
  form.setAttribute('aria-labelledby', buttonNode.id);
  if (moveFocus) buttonNode.focus();
  render();
}

function showProblems(problems) {
  const count = String(problems.length);
  if ($('#problem-count').textContent !== count) {
    clearTimeout(announceTimer);
    announceTimer = setTimeout(() => { $('#problem-status').textContent = problems.length ? count + ' validation problems' : 'No validation problems'; }, 1000);
  }
  $('#problem-count').textContent = count;
  const list = $('#problem-list');
  list.replaceChildren();
  if (!problems.length) list.append(el('li', 'ok', 'No parser rule problems found.'));
  else problems.forEach((problem) => list.append(el('li', '', (problem.line ? 'Line ' + problem.line + ': ' : '') + problem.message)));
}

function sync() {
  yamlBox.value = writeSource(sourceDoc, config);
  const parsed = parseSource(yamlBox.value);
  sourceDoc = parsed.doc ?? null;
  bindModel(sourceDoc, config);
  sourceDirty = false;
  $('#edit-status').textContent = 'Synced';
  $('#source-note').hidden = true;
  showProblems(validate(parsed, options));
  updatePreview();
}

function lockForm(locked) {
  form.inert = locked;
  form.classList.toggle('locked', locked);
  $('#form-lock').hidden = !locked;
}

function applySource() {
  clearTimeout(pendingApply);
  pendingApply = 0;
  const result = parseSource(yamlBox.value);
  if (result.error) {
    lockForm(true);
    $('#edit-status').textContent = 'Fix YAML';
    showProblems([result.error]);
    return false;
  }
  config = result.config;
  sourceDoc = result.doc;
  const renamed = canonicalizeKeys(config, options.conditions);
  $('#source-note').hidden = !renamed.length;
  $('#source-note').textContent = renamed.length ? 'The form uses the documented key spelling (' + renamed.join(', ') + '); the source is updated on the next form edit.' : '';
  sourceDirty = false;
  lockForm(false);
  $('#edit-status').textContent = 'Loaded';
  showProblems(validate(result, options));
  render();
  return true;
}

function flushSource() {
  if (pendingApply) applySource();
}

function allEntries() {
  const when = mappingOr(config.when);
  const lists = [when.general, when.sleep, ...['biome', 'death', 'enemy'].flatMap((bucket) => Object.values(mappingOr(when[bucket])))];
  return lists.flatMap((list) => Array.isArray(list) ? list : Array.isArray(list?.entries) ? list.entries : [])
    .map((entry) => typeof entry === 'string' ? { image: entry } : entry)
    .filter(isMapping);
}

function updatePreview() {
  const select = $('#preview-layout');
  const layouts = mappingOr(config.layouts);
  const names = Object.keys(layouts);
  if (!names.includes(previewName)) previewName = names[0] || '';
  select.replaceChildren();
  names.forEach((name) => { const option = el('option', '', name); option.value = name; select.append(option); });
  if (!names.length) { const option = el('option', '', 'No layouts'); option.value = ''; select.append(option); }
  select.value = previewName;
  const layout = Array.isArray(layouts[previewName]) ? { layers: layouts[previewName] } : mappingOr(layouts[previewName]);
  const entries = allEntries();
  const entry = entries.find((candidate) => candidate.layout === previewName) || entries[0] || {};
  try {
    drawPreview($('#screen'), layout, entry, mappingOr(config.defaults), images, $('#ratio').value);
  } catch (error) {
    console.error('Editor: the preview could not be drawn', error);
    $('#screen').replaceChildren(el('div', 'layer placeholder', 'The preview cannot show this layout'));
  }
}

const tabButtons = [...document.querySelectorAll('[data-tab]')];
tabButtons.forEach((buttonNode, index) => {
  buttonNode.addEventListener('click', () => selectTab(buttonNode));
  buttonNode.addEventListener('keydown', (event) => {
    const step = { ArrowRight: 1, ArrowLeft: -1 }[event.key];
    const target = step ? tabButtons[(index + step + tabButtons.length) % tabButtons.length]
      : event.key === 'Home' ? tabButtons[0] : event.key === 'End' ? tabButtons.at(-1) : null;
    if (!target) return;
    event.preventDefault();
    selectTab(target, true);
  });
});
form.addEventListener('focusin', flushSource, true);
form.addEventListener('pointerdown', flushSource, true);
yamlBox.addEventListener('input', () => {
  sourceDirty = true;
  $('#edit-status').textContent = 'YAML edited';
  clearTimeout(pendingApply);
  pendingApply = setTimeout(applySource, 300);
});
$('#file-input').addEventListener('change', async (event) => {
  const file = event.target.files[0];
  event.target.value = '';
  if (!file) return;
  if (file.size > MAX_SOURCE_LENGTH) {
    showProblems([{ line: 1, message: 'the file is larger than ' + MAX_SOURCE_LENGTH / 1024 + ' KB, the editor does not load it' }]);
    return;
  }
  yamlBox.value = await file.text();
  sourceDirty = true;
  applySource();
});
$('#download').addEventListener('click', () => {
  if ((sourceDirty || form.inert) && !applySource()) return;
  const blob = new Blob([yamlBox.value.endsWith('\n') ? yamlBox.value : yamlBox.value + '\n'], { type: 'text/yaml;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = el('a');
  link.href = url;
  link.download = 'loadingscreen.yaml';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
});
$('#preview-layout').addEventListener('change', (event) => { previewName = event.target.value; updatePreview(); });
$('#ratio').addEventListener('change', updatePreview);
$('#images').addEventListener('change', (event) => {
  images.forEach((url) => URL.revokeObjectURL(url));
  images = new Map();
  const list = $('#image-list');
  list.replaceChildren();
  for (const file of event.target.files) {
    images.set(file.name.toLowerCase(), URL.createObjectURL(file));
    list.append(el('li', '', file.name));
  }
  updatePreview();
});
new ResizeObserver(updatePreview).observe($('#screen'));
render();
sync();
