const mapping = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const text = (value) => value === undefined || value === null ? '' : String(value);
const color = (value) => /^#?[0-9a-f]{6}(?:[0-9a-f]{2})?$/i.test(text(value));
const length = (value) => /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:%|px)?$/i.test(text(value).trim());
const number = (value) => value !== '' && Number.isFinite(Number(value));
const boolean = (value) => ['true', 'false'].includes(text(value).trim().toLowerCase());
const names = (values) => new Set(values.map((value) => value.toLowerCase()));

export function validate(config, source, options) {
  const problems = [];
  const lines = source.split(/\r?\n/);
  function lineFor(key, value) {
    const escaped = String(key).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = new RegExp('(?:^|[\\s,{])["\']?' + escaped + '["\']?\\s*:');
    const valueText = text(value);
    const found = lines.findIndex((line) => match.test(line) && (!valueText || line.includes(valueText)));
    if (found >= 0) return found + 1;
    const fallback = lines.findIndex((line) => match.test(line));
    return fallback >= 0 ? fallback + 1 : undefined;
  }
  const warn = (message, key, value) => problems.push({ message, line: key ? lineFor(key, value) : undefined });
  function unknown(object, allowed, where) {
    if (!mapping(object)) return;
    const keys = names(allowed);
    for (const key of Object.keys(object)) if (!keys.has(key.toLowerCase())) warn(where + ": unknown key '" + key + "'", key);
  }
  function validColor(value, where, key, allowAuto = false) {
    if (value !== undefined && value !== null && !color(value) && !(allowAuto && text(value).toLowerCase() === 'auto'))
      warn(where + ": invalid " + key + " '" + value + "', use #RRGGBB or #RRGGBBAA", key, value);
  }
  function opacity(value, where, key = 'opacity') {
    if (value !== undefined && (!number(value) || Number(value) < 0 || Number(value) > 1))
      warn(where + ": '" + key + "' must be a number from 0 to 1", key, value);
  }
  function anchor(value, where, key = 'anchor') {
    if (value === undefined) return;
    const compact = text(value).replace(/[-_ ]/g, '').toLowerCase();
    if (!options.anchors.some((name) => name.replace(/-/g, '') === compact))
      warn(where + ": invalid anchor '" + value + "', use center, left, right, top, bottom or e.g. top-left", key, value);
  }
  function checkLength(value, where, key, allowNegative = true) {
    if (value === undefined) return;
    if (!length(value)) warn(where + ": invalid " + key + " '" + value + "', use a number with % or px", key, value);
    else if (!allowNegative && parseFloat(value) < 0) warn(where + ': ' + key + ' must not be negative', key, value);
  }
  function place(value, where, allowAnimate = false) {
    if (value === undefined) return;
    if (!mapping(value)) { warn(where + ': expected a mapping', 'place'); return; }
    unknown(value, allowAnimate ? ['fit', 'position', 'opacity', 'fade', 'animate'] : ['fit', 'position', 'opacity', 'fade'], where);
    const fit = value.fit;
    if (fit !== undefined) {
      if (mapping(fit)) {
        unknown(fit, ['width', 'height'], where + ": 'fit'");
        checkLength(fit.width, where, 'width', false);
        checkLength(fit.height, where, 'height', false);
        if (fit.width === undefined && fit.height === undefined) warn(where + ": 'fit' needs 'width' and/or 'height'", 'fit');
      } else if (!['cover', 'contain', 'stretch', 'none'].includes(text(fit).toLowerCase()))
        warn(where + ": invalid fit '" + fit + "', use cover, contain, stretch, none or { width, height }", 'fit', fit);
    }
    const position = value.position;
    if (position !== undefined) {
      if (mapping(position)) {
        unknown(position, ['anchor', 'x', 'y'], where + ": 'position'");
        anchor(position.anchor, where);
        checkLength(position.x, where, 'x');
        checkLength(position.y, where, 'y');
      } else if (typeof position === 'string') anchor(position, where, 'position');
      else warn(where + ": 'position' must be an anchor name or a mapping", 'position');
    }
    opacity(value.opacity, where);
    if (value.fade !== undefined) {
      const fade = value.fade, fadeWhere = where + ": 'fade'";
      if (!mapping(fade)) warn(where + ": 'fade' must be a mapping", 'fade');
      else {
        unknown(fade, ['sides', 'color', 'size', 'curve'], fadeWhere);
        const sides = Array.isArray(fade.sides) ? fade.sides : fade.sides === undefined ? [] : [fade.sides];
        sides.forEach((side) => { if (!['left', 'right', 'top', 'bottom', 'all'].includes(text(side).toLowerCase())) warn(fadeWhere + ": invalid side '" + side + "'", 'sides'); });
        if (!sides.length) warn(fadeWhere + " needs 'sides' (left, right, top, bottom or all), ignored", 'fade');
        if (fade.color !== undefined && !color(fade.color)) warn(fadeWhere + ": invalid color '" + fade.color + "', use #RRGGBB or #RRGGBBAA", 'color', fade.color);
        checkLength(fade.size, fadeWhere, 'size', false);
        if (fade.curve !== undefined && !['smooth', 'linear'].includes(text(fade.curve).toLowerCase()))
          warn(fadeWhere + ": invalid curve '" + fade.curve + "', use linear or smooth", 'curve', fade.curve);
      }
    }
    if (allowAnimate && value.animate !== undefined && !boolean(value.animate))
      warn(where + ": 'animate' must be true or false", 'animate');
  }
  function animation(value, where) {
    if (value === undefined) return;
    if (typeof value === 'string') {
      if (!value.trim()) warn(where + ': preset name cannot be empty', 'animation');
      return;
    }
    if (!mapping(value)) { warn(where + ': expected a preset name or phase mapping', 'animation'); return; }
    unknown(value, ['enter', 'idle', 'exit', 'speed', 'origin'], where);
    ['enter', 'idle', 'exit'].forEach((phase) => {
      if (value[phase] !== undefined && typeof value[phase] !== 'string')
        warn(where + ": '" + phase + "' must be a preset name", phase);
    });
    if (value.speed !== undefined && (!number(value.speed) || Number(value.speed) <= 0))
      warn(where + ': speed must be positive', 'speed', value.speed);
    anchor(value.origin, where + ": 'origin'", 'origin');
  }
  function effect(value, where, kind) {
    if (value === undefined) return;
    if (!mapping(value)) { warn(where + ': expected a mapping; effect ignored', kind); return; }
    unknown(value, kind === 'vignette' ? ['color', 'size', 'strength'] : ['color', 'opacity'], where);
    if (value.color !== undefined && !color(value.color)) validColor(value.color, where, 'color');
    if (kind === 'vignette') {
      if (value.size !== undefined && (!/^\d+(?:\.\d+)?%$/.test(text(value.size)) || parseFloat(value.size) > 100))
        warn(where + ": 'size' must be from 0% to 100%", 'size', value.size);
      opacity(value.strength, where, 'strength');
    } else opacity(value.opacity, where);
  }
  function asset(value, where, key, audio = false) {
    if (value === undefined) return;
    if (typeof value !== 'string' || !value.trim()) { warn(where + ': empty image path, ignored', key); return; }
    if (/^(?:[a-z]:[\\/]|[\\/])|(?:^|[\\/])\.\.(?:[\\/]|$)/i.test(value))
      warn(where + ": image path '" + value + "' must be relative to the config folder, ignored", key, value);
    else if (!(audio ? /\.(?:ogg|wav)$/i : /\.(?:png|jpe?g)$/i).test(value))
      warn(audio ? where + ": unsupported audio type '" + value + "', use .ogg or .wav" : where + ": unsupported image type '" + value + "', use .png, .jpg, .jpeg", key, value);
  }
  function layer(value, layoutName, index) {
    const where = "layout '" + layoutName + "' layer '" + (value?.id ?? index) + "'";
    if (!mapping(value)) { warn(where + ': a layer must be a mapping, ignored', 'layers'); return; }
    unknown(value, ['id', 'color', 'image', 'text', 'spinner', 'progress', 'size', 'align', 'background', 'place', 'animation'], where);
    place(value.place, where + ": 'place'", true);
    animation(value.animation, where + '.animation');
    const content = ['image', 'text', 'spinner', 'progress'].filter((key) => value[key] !== undefined);
    if (content.length > 1) warn(where + ": use only one of 'image', 'text', 'spinner' and 'progress' in a layer, ignored", content[1]);
    if (!content.length && value.color === undefined) warn(where + ": a layer needs 'image', 'text' or 'color', ignored", 'layers');
    if (value.progress !== undefined && !['bar', 'ring', 'percent'].includes(text(value.progress).toLowerCase()))
      warn(where + ": 'progress' needs bar, ring or percent; layer ignored", 'progress', value.progress);
    if (value.spinner !== undefined && (typeof value.spinner !== 'string' || !value.spinner.trim()))
      warn(where + ": 'spinner' needs hammer, ring, dots or an image path; layer ignored", 'spinner');
    if (value.spinner !== undefined && value.color !== undefined)
      warn(where + ": use only one of 'color' and 'spinner' in a layer, ignored", 'spinner');
    if (value.spinner && !['hammer', 'ring', 'dots'].includes(text(value.spinner).toLowerCase())) asset(value.spinner, where, 'spinner');
    if (value.text !== undefined && !['caption', 'prompt'].includes(text(value.text).toLowerCase()))
      warn(where + ": 'text' needs caption or prompt, layer ignored", 'text', value.text);
    if (value.text !== undefined && value.place?.fit !== undefined && !mapping(value.place.fit))
      warn(where + ": a text layer supports only 'fit: { width }' (the text width)", 'fit');
    if (value.text === undefined && value.progress === undefined)
      ['size', 'align', 'background'].forEach((key) => { if (value[key] !== undefined) warn(where + ": '" + key + "' applies only to text layers", key); });
    if (value.size !== undefined && (!number(value.size) || Number(value.size) <= 0))
      warn(where + (value.text !== undefined ? ": 'size' must be a positive font size" : ": 'size' must be positive"), 'size', value.size);
    if (value.align !== undefined && !['left', 'center', 'right'].includes(text(value.align).toLowerCase()))
      warn(where + ": invalid align '" + value.align + "', use left, center or right", 'align', value.align);
    ['color', 'background'].forEach((key) => { if (value[key] !== undefined && (value.text !== undefined || value.progress !== undefined || value.image === undefined)) validColor(value[key], where, key, value.text !== undefined); });
    if (value.image !== undefined) {
      const values = Array.isArray(value.image) ? value.image : [value.image];
      values.forEach((image) => { if (Array.isArray(value.image) || !['entry', 'separator'].includes(text(image).toLowerCase())) asset(image, where, 'image'); });
      if (value.color !== undefined && text(value.image).toLowerCase() === 'separator') validColor(value.color, where, 'color');
      else if (value.color !== undefined) warn(where + ": 'color' is ignored on an image layer", 'color');
    }
  }
  function entry(value, where) {
    if (typeof value === 'string') { asset(value, where, 'image'); return; }
    if (!mapping(value)) { warn(where + ': an entry must be an image path or a mapping, ignored', 'entries'); return; }
    unknown(value, ['image', 'sound', 'voice', 'text', 'layout', 'place', 'when'], where);
    if (value.image === undefined && value.text === undefined) warn(where + ": an entry needs an 'image' or a 'text', ignored", 'entries');
    if (value.image !== undefined) asset(value.image, where, 'image');
    if (value.sound !== undefined) asset(value.sound, where, 'sound', true);
    if (value.voice !== undefined) asset(value.voice, where, 'voice', true);
    if (value.text !== undefined) {
      if (mapping(value.text)) {
        Object.entries(value.text).forEach(([code, translation]) => {
          if (!/^[a-z]{2,3}(?:-[a-z]{2,4})?$/i.test(code)) warn(where + ": unknown text language '" + code + "'", code);
          if (typeof translation !== 'string' || !translation.trim()) warn(where + ": text for '" + code + "' must be a non-empty string", code);
        });
      } else if (typeof value.text !== 'string') warn(where + ": 'text' must be a string or a language mapping", 'text');
    }
    place(value.place, where + ": 'place'");
    if (value.when !== undefined) {
      const conditionWhere = where + ": 'when'";
      if (!mapping(value.when)) warn(conditionWhere + ': expected a mapping, entry ignored', 'when');
      else Object.entries(value.when).forEach(([key, namesValue]) => {
        if (!options.conditions.some((condition) => condition.toLowerCase() === key.toLowerCase()))
          warn(conditionWhere + ": unknown condition '" + key + "', entry ignored", key);
        const values = Array.isArray(namesValue) ? namesValue : [namesValue];
        values.forEach((name) => { if (typeof name !== 'string' || !name.trim()) warn(conditionWhere + ": '" + key + "' needs a name, entry ignored", key); });
      });
    }
    if (value.layout && !Object.keys(config.layouts || {}).some((name) => name.toLowerCase() === text(value.layout).toLowerCase()))
      warn(where + ": unknown layout '" + value.layout + "', the default layout is used", 'layout', value.layout);
  }
  function list(value, where) {
    const object = mapping(value) ? value : { entries: value };
    if (mapping(value)) unknown(value, ['layout', 'sound', 'order', 'entries'], where);
    if (object.order !== undefined && !['random', 'sequential'].includes(text(object.order).toLowerCase()))
      warn("'" + where + "' order: use 'random' or 'sequential', got '" + object.order + "'", 'order', object.order);
    if (object.sound !== undefined) asset(object.sound, where, 'sound', true);
    if (object.layout && !Object.keys(config.layouts || {}).some((name) => name.toLowerCase() === text(object.layout).toLowerCase()))
      warn(where + ": unknown layout '" + object.layout + "', the default layout is used", 'layout', object.layout);
    if (!Array.isArray(object.entries)) warn(where + ' must be a list', 'entries');
    else object.entries.forEach((item, index) => entry(item, where + '.entries[' + index + ']'));
  }

  if (!mapping(config)) return [{ line: 1, message: 'the top level must be a mapping, config ignored' }];
  const topKeys = names(['defaults', 'layouts', 'order', 'when']);
  Object.keys(config).forEach((key) => { if (!topKeys.has(key.toLowerCase())) warn("unknown top-level key '" + key + "'", key); });
  if (config.order !== undefined && !['random', 'sequential'].includes(text(config.order).toLowerCase()))
    warn("order: use 'random' or 'sequential', got '" + config.order + "'", 'order', config.order);
  if (config.defaults !== undefined) {
    const defaults = config.defaults;
    if (!mapping(defaults)) warn('defaults: expected a mapping', 'defaults');
    else {
      unknown(defaults, ['place', 'background', 'tint', 'vignette', 'mask', 'animation', 'tip', 'continuePrompt', 'separator', 'spinner', 'zoom'], 'defaults');
      place(defaults.place, 'defaults.place', true);
      ['background', 'tint'].forEach((key) => { if (defaults[key] !== undefined && !color(defaults[key])) warn('defaults.' + key + ': invalid color, use #RRGGBB or #RRGGBBAA; built-in value used', key); });
      animation(defaults.animation, 'defaults.animation');
      effect(defaults.vignette, 'defaults.vignette', 'vignette');
      effect(defaults.mask, 'defaults.mask', 'mask');
      ['tip', 'continuePrompt', 'separator', 'spinner'].forEach((section) => {
        const value = defaults[section];
        if (value === undefined) return;
        if (!mapping(value)) { warn('defaults.' + section + ': expected a mapping; built-in values used', section); return; }
        const allowed = section === 'tip' || section === 'continuePrompt' ? ['x', 'bottom', 'size', 'color'] : section === 'separator' ? ['show', 'x', 'bottom', 'onLayouts'] : ['center', 'x', 'bottom'];
        unknown(value, allowed, 'defaults.' + section);
        if (value.size !== undefined && (!Number.isInteger(Number(value.size)) || Number(value.size) < 0 || Number(value.size) > 64))
          warn('defaults.' + section + '.size: must be an integer from 0 to 64; built-in value used', 'size', value.size);
        if (value.color !== undefined && !color(value.color)) warn('defaults.' + section + '.color: invalid color, use #RRGGBB or #RRGGBBAA; built-in value used', 'color');
      });
    }
  }
  if (config.layouts !== undefined) {
    if (!mapping(config.layouts)) warn("'layouts' must be a mapping of layout names", 'layouts');
    else Object.entries(config.layouts).forEach(([name, value]) => {
      if (!mapping(value)) { warn("layout '" + name + "' needs a 'layers' list, ignored", name); return; }
      unknown(value, ['layers', 'sound', 'showSleepUi', 'vignette', 'mask', 'place'], "layout '" + name + "'");
      place(value.place, "layout '" + name + "': 'place'");
      effect(value.vignette, "layout '" + name + "'.vignette", 'vignette');
      effect(value.mask, "layout '" + name + "'.mask", 'mask');
      if (value.sound !== undefined) asset(value.sound, "layout '" + name + "'", 'sound', true);
      if (value.showSleepUi !== undefined && !boolean(value.showSleepUi))
        warn("layout '" + name + "': 'showSleepUi' must be true or false", 'showSleepUi');
      if (!Array.isArray(value.layers)) warn("layout '" + name + "' needs a 'layers' list, ignored", 'layers');
      else {
        value.layers.forEach((item, index) => layer(item, name, index));
        if (!value.layers.some((item) => mapping(item) && item.image === 'entry'))
          warn("layout '" + name + "' has no 'image: entry' layer, so entry images are not shown with it");
      }
    });
  }
  if (config.when !== undefined) {
    if (!mapping(config.when)) warn("'when' must be a mapping", 'when');
    else Object.entries(config.when).forEach(([key, value]) => {
      if (['general', 'sleep'].includes(key.toLowerCase())) list(value, key);
      else if (['biome', 'death', 'enemy'].includes(key.toLowerCase())) {
        if (!mapping(value)) warn("'" + key + "' must be a mapping of names to entries", key);
        else Object.entries(value).forEach(([name, item]) => {
          if (key.toLowerCase() === 'death' && !options.deathCauses.some((candidate) => candidate.toLowerCase() === name.toLowerCase()))
            warn("unknown death name '" + name + "', known: " + options.deathCauses.join(', '), name);
          list(item, key + '.' + name);
        });
      } else warn("unknown situation '" + key + "', use 'general', 'sleep', 'biome', 'death' or 'enemy'", key);
    });
  }
  return problems;
}
