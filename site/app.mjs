import { parseSource, canonicalizeKeys, define, renameKey, writeSource, MAX_SOURCE_LENGTH } from './yaml-model.mjs';
import { validate } from './validate.mjs';
import { drawPreview } from './preview.mjs';

const $ = (selector) => document.querySelector(selector);
const form = $('#form');
const yamlBox = $('#yaml');
const options = await fetch('./data/options.json').then((response) => response.json());
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
  let input;
  if (multiline) input = el('textarea');
  else if (values) {
    input = el('select');
    values.forEach((choice) => {
      const option = el('option', '', choice);
      option.value = choice;
      input.append(option);
    });
  } else input = el('input');
  input.id = id;
  if (!values && !multiline) input.type = type;
  if (list) {
    const data = el('datalist');
    data.id = id + '-list';
    list.forEach((choice) => {
      const option = el('option');
      option.value = choice;
      data.append(option);
    });
    input.setAttribute('list', data.id);
    wrap.append(data);
  }
  input.placeholder = placeholder;
  input.value = value === undefined || value === null ? '' : String(value);
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
  button(actions, 'Remove', remove, true);
  append(head, actions);
  append(node, head);
  parent.append(node);
  return node;
}

function set(target, key, value, numeric = false) {
  if (value === '') delete target[key];
  else target[key] = numeric && Number.isFinite(Number(value)) ? Number(value) : value;
}

function placeEditor(parent, owner, title) {
  const box = group(parent, title);
  const place = owner.place || {};
  const fit = place.fit;
  const fitMode = typeof fit === 'object' && fit !== null ? 'box' : fit || '';
  const fields = grid(box);
  field(fields, 'Fit', fitMode, (v) => { owner.place = place; if (!v) delete place.fit; else place.fit = v === 'box' ? { width: '50%' } : v; render(); }, { values: ['', 'contain', 'cover', 'stretch', 'none', 'box'] });
  if (fitMode === 'box') {
    field(fields, 'Box width', fit.width, (v) => { owner.place = place; set(fit, 'width', v); }, { placeholder: '50%' });
    field(fields, 'Box height', fit.height, (v) => { owner.place = place; set(fit, 'height', v); }, { placeholder: '250px' });
  }
  const position = typeof place.position === 'string' ? { anchor: place.position } : place.position ?? {};
  if (typeof place.position === 'string') place.position = position;
  field(fields, 'Anchor', position.anchor || '', (v) => { owner.place = place; place.position = position; set(position, 'anchor', v); }, { list: options.anchors, placeholder: 'center' });
  field(fields, 'Offset X', position.x || '', (v) => { owner.place = place; place.position = position; set(position, 'x', v); }, { placeholder: '0px or 10%' });
  field(fields, 'Offset Y', position.y || '', (v) => { owner.place = place; place.position = position; set(position, 'y', v); }, { placeholder: '0px or -10%' });
  field(fields, 'Opacity', place.opacity, (v) => { owner.place = place; set(place, 'opacity', v, true); }, { type: 'number', placeholder: '0–1' });
  const fade = place.fade || {};
  const fadeBox = group(box, 'Edge fade');
  const fadeFields = grid(fadeBox);
  field(fadeFields, 'Sides', Array.isArray(fade.sides) ? fade.sides.join(', ') : fade.sides || '', (v) => { owner.place = place; if (!v) delete place.fade; else { place.fade = fade; fade.sides = v.split(',').map((s) => s.trim()).filter(Boolean); } }, { placeholder: 'bottom, left' });
  field(fadeFields, 'Fade color', fade.color || '', (v) => { owner.place = place; place.fade = fade; set(fade, 'color', v); }, { placeholder: '#000000' });
  field(fadeFields, 'Fade size', fade.size || '', (v) => { owner.place = place; place.fade = fade; set(fade, 'size', v); }, { placeholder: '15%' });
  field(fadeFields, 'Curve', fade.curve || '', (v) => { owner.place = place; place.fade = fade; set(fade, 'curve', v); }, { values: ['', 'smooth', 'linear'] });
}

function animationEditor(parent, owner) {
  const box = group(parent, 'Animation');
  let animation = owner.animation;
  if (typeof animation === 'string') animation = owner.animation = { idle: animation };
  animation ||= {};
  const fields = grid(box);
  ['enter', 'idle', 'exit'].forEach((phase) => field(fields, phase[0].toUpperCase() + phase.slice(1), animation[phase] || '', (v) => { owner.animation = animation; set(animation, phase, v); }, { list: options.animations, placeholder: 'none' }));
  field(fields, 'Speed', animation.speed, (v) => { owner.animation = animation; set(animation, 'speed', v, true); }, { type: 'number', placeholder: '1' });
  field(fields, 'Origin', animation.origin || '', (v) => { owner.animation = animation; set(animation, 'origin', v); }, { list: options.anchors, placeholder: 'center' });
}

function effectsEditor(parent, owner) {
  const box = group(parent, 'Screen effects');
  const fields = grid(box);
  const vignette = owner.vignette || {};
  field(fields, 'Vignette color', vignette.color || '', (v) => { owner.vignette = vignette; set(vignette, 'color', v); }, { placeholder: '#000000' });
  field(fields, 'Vignette size', vignette.size || '', (v) => { owner.vignette = vignette; set(vignette, 'size', v); }, { placeholder: '25%' });
  field(fields, 'Vignette strength', vignette.strength, (v) => { owner.vignette = vignette; set(vignette, 'strength', v, true); }, { type: 'number', placeholder: '0–1' });
  const mask = owner.mask || {};
  field(fields, 'Mask color', mask.color || '', (v) => { owner.mask = mask; set(mask, 'color', v); }, { placeholder: '#000000' });
  field(fields, 'Mask opacity', mask.opacity, (v) => { owner.mask = mask; set(mask, 'opacity', v, true); }, { type: 'number', placeholder: '0–1' });
}

function defaultsForm() {
  const intro = group(form, 'Base appearance', 'These values apply when a layout or layer does not override them.');
  const fields = grid(intro);
  const defaults = config.defaults ??= {};
  field(fields, 'Background', defaults.background || '', (v) => set(defaults, 'background', v), { placeholder: '#000000' });
  field(fields, 'Image tint', defaults.tint || '', (v) => set(defaults, 'tint', v), { placeholder: '#FFFFFF' });
  field(fields, 'Global entry order', config.order || '', (v) => set(config, 'order', v), { values: ['', 'random', 'sequential'] });
  placeEditor(form, defaults, 'Default placement');
  animationEditor(form, defaults);
  effectsEditor(form, defaults);
  ['tip', 'continuePrompt', 'separator', 'spinner'].forEach((name) => {
    const section = group(form, name === 'continuePrompt' ? 'Continue prompt' : name[0].toUpperCase() + name.slice(1));
    const object = defaults[name] || {};
    const row = grid(section);
    ['x', 'bottom'].forEach((key) => field(row, key === 'x' ? 'Offset X (px)' : 'Bottom (px)', object[key], (v) => { defaults[name] = object; set(object, key, v, true); }, { type: 'number' }));
    if (name === 'tip' || name === 'continuePrompt') {
      field(row, 'Font size', object.size, (v) => { defaults[name] = object; set(object, 'size', v, true); }, { type: 'number' });
      field(row, 'Color', object.color || '', (v) => { defaults[name] = object; set(object, 'color', v); });
    }
    if (name === 'separator') field(row, 'Show', object.show === undefined ? '' : String(object.show), (v) => { defaults[name] = object; set(object, 'show', v === '' ? '' : v === 'true'); }, { values: ['', 'true', 'false'] });
    if (name === 'spinner') field(row, 'Center', object.center === undefined ? '' : String(object.center), (v) => { defaults[name] = object; set(object, 'center', v === '' ? '' : v === 'true'); }, { values: ['', 'true', 'false'] });
  });
}

function isMapping(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function languageName(code) {
  const lower = code.trim().toLowerCase();
  const match = Object.entries(options.languages).find(([language, value]) => value.toLowerCase() === lower || language.toLowerCase() === lower);
  return match ? match[0].replace(/_/g, ' ') + ' (' + match[1] + ')' : code;
}

function layerKind(layer) {
  for (const name of ['image', 'text', 'spinner', 'progress', 'color']) if (Object.hasOwn(layer, name)) return name;
  return 'image';
}

function layerForm(parent, layer, layout, index) {
  const kind = layerKind(layer);
  const item = card(parent, 'Layer ' + (index + 1) + ' · ' + kind, () => { layout.layers.splice(index, 1); render(); sync(); }, (direction) => {
    const next = index + direction;
    if (next >= 0 && next < layout.layers.length) {
      [layout.layers[index], layout.layers[next]] = [layout.layers[next], layout.layers[index]];
      render(); sync();
    }
  });
  const fields = grid(item);
  field(fields, 'Kind', kind, (v) => {
    ['image', 'text', 'spinner', 'progress', 'color'].forEach((key) => delete layer[key]);
    delete layer.size; delete layer.align; delete layer.background;
    layer[v] = { image: 'entry', text: 'caption', spinner: 'ring', progress: 'bar', color: '#000000' }[v];
    render();
  }, { values: ['image', 'text', 'spinner', 'progress', 'color'] });
  field(fields, 'Layer ID', layer.id || '', (v) => set(layer, 'id', v), { placeholder: 'optional' });
  if (kind === 'image') field(fields, 'Image path or keyword', Array.isArray(layer.image) ? layer.image.join('\n') : layer.image || '', (v) => { layer.image = v.includes('\n') ? v.split('\n').map((s) => s.trim()).filter(Boolean) : v; }, { multiline: true, wide: true, placeholder: 'entry, separator, or images/logo.png\nOne path per line for alternatives' });
  if (kind === 'text') {
    field(fields, 'Text source', layer.text, (v) => { layer.text = v; }, { values: ['caption', 'prompt'] });
    field(fields, 'Font size', layer.size, (v) => set(layer, 'size', v, true), { type: 'number' });
    field(fields, 'Text color', layer.color || '', (v) => set(layer, 'color', v), { placeholder: '#FFFFFF or auto' });
    field(fields, 'Box background', layer.background || '', (v) => set(layer, 'background', v), { placeholder: '#00000099 or auto' });
    field(fields, 'Align', layer.align || '', (v) => set(layer, 'align', v), { values: ['', 'left', 'center', 'right'] });
  }
  if (kind === 'spinner') field(fields, 'Spinner style or image path', layer.spinner || '', (v) => set(layer, 'spinner', v), { list: ['hammer', 'ring', 'dots'] });
  if (kind === 'progress') {
    field(fields, 'Progress style', layer.progress || '', (v) => set(layer, 'progress', v), { values: ['bar', 'ring', 'percent'] });
    field(fields, 'Size', layer.size, (v) => set(layer, 'size', v, true), { type: 'number' });
    field(fields, 'Color', layer.color || '', (v) => set(layer, 'color', v));
  }
  if (kind === 'color') field(fields, 'Fill color', layer.color || '', (v) => set(layer, 'color', v), { placeholder: '#000000' });
  placeEditor(item, layer, 'Placement');
  animationEditor(item, layer);
}

function layoutsForm() {
  const head = group(form, 'Layouts', 'Layers draw in order, from back to front. Use image: entry to show an entry’s image.');
  button(head, '+ Add layout', () => {
    const name = uniqueName(config.layouts ??= {}, 'new-layout');
    config.layouts[name] = { layers: [{ image: 'entry', place: { fit: 'cover' } }] };
    previewName = name; render(); sync();
  });
  Object.entries(config.layouts ??= {}).forEach(([name, layout]) => {
    const item = card(form, name, () => { delete config.layouts[name]; render(); sync(); });
    const fields = grid(item);
    field(fields, 'Layout name', name, (v) => {
      const next = v.trim();
      if (next && next !== name && !config.layouts[next]) {
        renameKey(config.layouts, name, next);
        if (previewName === name) previewName = next;
        render();
      }
    }, { onChange: true });
    field(fields, 'Looping sound', layout.sound || '', (v) => set(layout, 'sound', v), { placeholder: 'music/forest.ogg' });
    placeEditor(item, layout, 'Layout placement');
    effectsEditor(item, layout);
    const layers = group(item, 'Layers');
    button(layers, '+ Add layer', () => { (layout.layers ??= []).push({ image: 'entry' }); render(); sync(); });
    (layout.layers ??= []).forEach((layer, index) => layerForm(layers, layer, layout, index));
  });
}

function uniqueName(collection, base) {
  let name = base, suffix = 2;
  while (Object.hasOwn(collection, name)) name = base + '-' + suffix++;
  return name;
}

function entryForm(parent, entries, index) {
  const entry = typeof entries[index] === 'string' ? entries[index] = { image: entries[index] } : entries[index];
  const item = card(parent, 'Entry ' + (index + 1), () => { entries.splice(index, 1); render(); sync(); }, (direction) => {
    const next = index + direction;
    if (next >= 0 && next < entries.length) {
      [entries[index], entries[next]] = [entries[next], entries[index]];
      render(); sync();
    }
  });
  const fields = grid(item);
  field(fields, 'Image path', entry.image || '', (v) => set(entry, 'image', v), { placeholder: 'screens/example.jpg' });
  field(fields, 'Layout override', entry.layout || '', (v) => set(entry, 'layout', v), { list: Object.keys(config.layouts || {}), placeholder: 'Use list layout' });
  field(fields, 'Looping sound', entry.sound || '', (v) => set(entry, 'sound', v), { placeholder: 'music/forest.ogg' });
  field(fields, 'Voice', entry.voice || '', (v) => set(entry, 'voice', v), { placeholder: 'voice/welcome.wav' });
  const translated = isMapping(entry.text);
  field(fields, 'Text format', translated ? 'translations' : 'plain', (v) => {
    entry.text = v === 'translations' ? { en: typeof entry.text === 'string' ? entry.text : '' } : (entry.text?.en || Object.values(entry.text || {}).find((value) => typeof value === 'string') || '');
    render();
  }, { values: ['plain', 'translations'] });
  if (translated) {
    Object.keys(entry.text).forEach((code) => field(fields, 'Text · ' + languageName(code), entry.text[code] ?? '', (v) => set(entry.text, code, v), { multiline: true, wide: true }));
    const used = Object.keys(entry.text).map((code) => (options.languages[Object.keys(options.languages).find((language) => language.toLowerCase() === code.trim().toLowerCase())] ?? code).toLowerCase());
    const missing = [...new Set(Object.values(options.languages))].filter((code) => !used.includes(code.toLowerCase()));
    if (missing.length) field(fields, 'Add translation', '', (v) => { if (v) { define(entry.text, v.split(' ')[0], ''); render(); } }, { values: ['', ...missing.map((code) => code + ' · ' + languageName(code))], onChange: true });
  } else field(fields, 'Caption text', typeof entry.text === 'string' ? entry.text : '', (v) => set(entry, 'text', v), { multiline: true, wide: true });
  placeEditor(item, entry, 'Entry image placement');
  const conditions = group(item, 'Conditions', 'Every condition must match. A custom item or player key can be typed.');
  button(conditions, '+ Add condition', () => { const values = entry.when ??= {}; const key = options.conditions.find((name) => !Object.hasOwn(values, name)); if (key) values[key] = ''; render(); sync(); });
  Object.entries(entry.when || {}).forEach(([key, value]) => {
    const row = card(conditions, key, () => { delete entry.when[key]; render(); sync(); });
    const fields = grid(row);
    field(fields, 'Condition', key, (v) => {
      if (v && v !== key && !Object.hasOwn(entry.when, v)) {
        renameKey(entry.when, key, v);
        render();
      }
    }, { list: options.conditions, onChange: true });
    const suggestions = key.toLowerCase().includes('biome') ? options.biomes : key.toLowerCase().includes('global') ? options.globalKeys : [];
    field(fields, 'Name or comma-separated names', Array.isArray(value) ? value.join(', ') : value ?? '', (v) => { entry.when[key] = v.includes(',') ? v.split(',').map((s) => s.trim()).filter(Boolean) : v; }, { list: suggestions });
  });
}

function listForm(parent, bucket, name, named = false) {
  const lists = named ? (config.when[bucket] ??= {}) : config.when;
  let list = lists[name];
  if (Array.isArray(list)) list = lists[name] = { entries: list };
  else if (typeof list === 'string') list = lists[name] = { entries: [list] };
  list ??= lists[name] = { entries: [] };
  const item = card(parent, named ? bucket + ' · ' + name : name, () => { delete lists[name]; render(); sync(); });
  const fields = grid(item);
  if (named) field(fields, 'Name', name, (v) => {
    const next = v.trim();
    if (next && next !== name && !lists[next]) { renameKey(lists, name, next); render(); }
  }, { list: bucket === 'biome' ? options.biomes : bucket === 'death' ? options.deathCauses : [], onChange: true });
  field(fields, 'List layout', list.layout || '', (v) => set(list, 'layout', v), { list: Object.keys(config.layouts || {}), placeholder: 'Use default' });
  field(fields, 'Order', list.order || '', (v) => set(list, 'order', v), { values: ['', 'random', 'sequential'] });
  field(fields, 'Looping sound', list.sound || '', (v) => set(list, 'sound', v), { placeholder: 'music/forest.ogg' });
  const entries = group(item, 'Entries');
  button(entries, '+ Add entry', () => { (list.entries ??= []).push({ image: 'screens/image.jpg' }); render(); sync(); });
  (list.entries ??= []).forEach((entry, index) => entryForm(entries, list.entries, index));
}

function whenForm() {
  config.when ??= {};
  const intro = group(form, 'Situations', 'General covers ordinary loading. Biome covers teleport and death fallback; death covers hit and damage types.');
  ['general', 'sleep'].forEach((name) => {
    const section = group(form, name === 'general' ? 'General' : 'Sleep');
    if (config.when[name]) listForm(section, name, name);
    else button(section, '+ Add ' + name + ' list', () => { config.when[name] = { entries: [] }; render(); sync(); });
  });
  ['biome', 'death', 'enemy'].forEach((bucket) => {
    const section = group(form, bucket[0].toUpperCase() + bucket.slice(1) + ' lists');
    button(section, '+ Add ' + bucket + ' list', () => {
      const names = bucket === 'biome' ? options.biomes : bucket === 'death' ? options.deathCauses : ['Troll'];
      const collection = config.when[bucket] ??= {};
      const name = names.find((candidate) => !Object.hasOwn(collection, candidate)) || uniqueName(collection, 'custom');
      collection[name] = { entries: [] }; render(); sync();
    });
    Object.keys(config.when[bucket] || {}).forEach((name) => listForm(section, bucket, name, true));
  });
}

function render() {
  form.replaceChildren();
  try {
    if (tab === 'defaults') defaultsForm();
    if (tab === 'layouts') layoutsForm();
    if (tab === 'when') whenForm();
  } catch {
    form.replaceChildren(el('p', 'section-note', 'This section has an invalid structure. Correct the YAML to edit it in the form.'));
    $('#edit-status').textContent = 'Fix YAML';
  }
  updatePreview();
}

function showProblems(problems) {
  $('#problem-count').textContent = String(problems.length);
  const list = $('#problem-list');
  list.replaceChildren();
  if (!problems.length) list.append(el('li', 'ok', 'No parser rule problems found.'));
  else problems.forEach((problem) => list.append(el('li', '', (problem.line ? 'Line ' + problem.line + ': ' : '') + problem.message)));
}

function sync() {
  yamlBox.value = writeSource(sourceDoc, config);
  const parsed = parseSource(yamlBox.value);
  sourceDoc = parsed.doc ?? null;
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
  const when = config.when || {};
  const lists = [when.general, when.sleep, ...Object.values(when.biome || {}), ...Object.values(when.death || {}), ...Object.values(when.enemy || {})];
  return lists.flatMap((list) => Array.isArray(list) ? list : list?.entries || []).map((entry) => typeof entry === 'string' ? { image: entry } : entry);
}

function updatePreview() {
  const select = $('#preview-layout');
  const names = Object.keys(config.layouts || {});
  if (!names.includes(previewName)) previewName = names[0] || '';
  select.replaceChildren();
  names.forEach((name) => { const option = el('option', '', name); option.value = name; select.append(option); });
  if (!names.length) { const option = el('option', '', 'No layouts'); option.value = ''; select.append(option); }
  select.value = previewName;
  const layout = config.layouts?.[previewName];
  const entry = allEntries().find((candidate) => candidate.layout === previewName) || allEntries()[0] || {};
  drawPreview($('#screen'), layout, entry, config.defaults || {}, images, $('#ratio').value);
}

document.querySelectorAll('[data-tab]').forEach((buttonNode) => buttonNode.addEventListener('click', () => {
  flushSource();
  tab = buttonNode.dataset.tab;
  document.querySelectorAll('[data-tab]').forEach((node) => node.classList.toggle('active', node === buttonNode));
  render();
}));
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
  applySource();
});
$('#download').addEventListener('click', () => {
  if (sourceDirty && !applySource()) return;
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
