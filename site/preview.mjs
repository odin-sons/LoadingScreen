import { isColor } from './validate.mjs';

const node = (tag, className = '') => {
  const element = document.createElement(tag);
  element.className = className;
  return element;
};

const cssColor = (value, fallback) => typeof value === 'string' && isColor(value) ? '#' + value.trim().replace(/^#+/, '') : fallback;

const scalar = (value) => typeof value === 'string' || typeof value === 'number' ? value : undefined;

function resolveLength(value, reference, scale) {
  value = scalar(value);
  if (value === undefined) return 0;
  const text = String(value).trim();
  const amount = parseFloat(text);
  if (!Number.isFinite(amount)) return 0;
  return text.endsWith('%') ? reference * amount / 100 : amount * scale;
}

function dimensions(fit, kind, width, height, scale) {
  const fallback = kind === 'text' ? [width * .8, height * .12] : kind === 'spinner' ? [56 * scale, 56 * scale] : kind === 'progress' ? [Math.min(width * .4, 320 * scale), 48 * scale] : [width, height];
  if (fit && typeof fit === 'object') {
    const w = fit.width === undefined ? 0 : resolveLength(fit.width, width, scale);
    const h = fit.height === undefined ? 0 : resolveLength(fit.height, height, scale);
    if (kind === 'text') return [w || fallback[0], fallback[1]];
    if (w && h) return [w, h];
    if (w) return [w, w * 9 / 16];
    if (h) return [h * 16 / 9, h];
  }
  if (fit === 'none') return [Math.min(640 * scale, width), Math.min(360 * scale, height)];
  if (kind === 'image' && (fit === 'contain' || fit === 'cover' || fit === 'stretch')) return [width, height];
  return fallback;
}

function placement(entry, layer, layout, defaults) {
  const places = [entry?.place, layer?.place, layout?.place, defaults?.place].filter(Boolean);
  const value = {};
  for (const key of ['fit', 'position', 'opacity', 'fade']) {
    const source = places.find((item) => item[key] !== undefined);
    if (source) value[key] = source[key];
  }
  return value;
}

function position(element, place, width, height, itemWidth, itemHeight, scale) {
  const raw = place.position;
  const position = typeof raw === 'string' ? { anchor: raw } : raw || {};
  const anchor = String(scalar(position.anchor) || 'center').toLowerCase().replace(/[_ ]/g, '-');
  const horizontal = anchor.includes('left') || anchor === 'left' ? 0 : anchor.includes('right') || anchor === 'right' ? 1 : .5;
  const vertical = anchor.includes('top') || anchor === 'top' ? 0 : anchor.includes('bottom') || anchor === 'bottom' ? 1 : .5;
  element.style.left = (horizontal * (width - itemWidth) + resolveLength(position.x, width, scale)) + 'px';
  element.style.top = (vertical * (height - itemHeight) + resolveLength(position.y, height, scale)) + 'px';
  element.style.width = Math.max(1, itemWidth) + 'px';
  element.style.height = Math.max(1, itemHeight) + 'px';
  element.style.opacity = String(scalar(place.opacity) ?? 1);
}

function fade(element, value, width, height, scale) {
  if (!value || !value.sides) return;
  const sides = Array.isArray(value.sides) ? value.sides : [value.sides];
  const color = cssColor(value.color, '#000000');
  const size = value.size || '15%';
  for (const side of sides.flatMap((item) => item === 'all' ? ['left', 'right', 'top', 'bottom'] : [item])) {
    if (!['left', 'right', 'top', 'bottom'].includes(side)) continue;
    const overlay = node('div');
    const horizontal = side === 'left' || side === 'right';
    const distance = Math.min(horizontal ? width : height, Math.max(0, resolveLength(size, horizontal ? width : height, scale)));
    overlay.style.position = 'absolute';
    overlay.style.pointerEvents = 'none';
    overlay.style[side] = '0';
    overlay.style[horizontal ? 'top' : 'left'] = '0';
    overlay.style[horizontal ? 'height' : 'width'] = '100%';
    overlay.style[horizontal ? 'width' : 'height'] = distance + 'px';
    const direction = { left: 'to right', right: 'to left', top: 'to bottom', bottom: 'to top' }[side];
    overlay.style.background = 'linear-gradient(' + direction + ', ' + color + ', transparent)';
    element.append(overlay);
  }
}

function imagePath(value, entry) {
  if (Array.isArray(value)) value = value[0];
  return value === 'entry' ? entry.image : value;
}

export function drawPreview(screen, layout, entry, defaults, images, ratio) {
  screen.replaceChildren();
  screen.style.aspectRatio = ratio || '16/9';
  screen.style.background = cssColor(defaults.background, '#000000');
  if (!layout || !Array.isArray(layout.layers)) {
    const hint = node('div', 'layer placeholder');
    hint.textContent = 'Select a layout to preview';
    hint.style.inset = '0';
    screen.append(hint);
    return;
  }
  const width = screen.clientWidth || 960;
  const height = width / (ratio === '21/9' ? 21 / 9 : ratio === '16/10' ? 16 / 10 : 16 / 9);
  const scale = width / 1920;
  for (const layer of layout.layers) {
    if (!layer || typeof layer !== 'object') continue;
    const kind = layer.image !== undefined ? 'image' : layer.text !== undefined ? 'text' : layer.spinner !== undefined ? 'spinner' : layer.progress !== undefined ? 'progress' : 'color';
    const place = placement(kind === 'image' && layer.image === 'entry' ? entry : null, layer, layout, defaults);
    const [itemWidth, itemHeight] = dimensions(place.fit, kind, width, height, scale);
    const element = node('div', 'layer');
    position(element, place, width, height, itemWidth, itemHeight, scale);
    if (kind === 'color') {
      element.style.background = cssColor(layer.color, '#000000');
    } else if (kind === 'image') {
      const path = imagePath(layer.image, entry);
      if (path === 'separator') {
        element.classList.add('separator');
        element.style.background = 'linear-gradient(90deg,transparent,' + cssColor(layer.color, '#fff') + ',transparent)';
      } else {
        const url = path && images.get(String(scalar(path) ?? '').split(/[\\/]/).pop().toLowerCase());
        if (url) {
          const image = node('img');
          image.src = url;
          image.alt = '';
          image.style.width = '100%';
          image.style.height = '100%';
          image.style.objectFit = place.fit === 'cover' ? 'cover' : place.fit === 'stretch' ? 'fill' : 'contain';
          element.append(image);
        } else {
          element.classList.add('placeholder');
          element.textContent = scalar(path) ? String(path) : 'Entry image';
        }
      }
      fade(element, place.fade, itemWidth, itemHeight, scale);
    } else if (kind === 'text') {
      const content = layer.text === 'prompt' ? 'Press any key to continue' : entry.text && typeof entry.text === 'object' ? scalar(entry.text.en) || Object.values(entry.text).find(scalar) || 'Caption' : scalar(entry.text) || 'Caption preview';
      element.textContent = content;
      element.style.fontSize = Math.max(10, Number(layer.size || 28) * scale) + 'px';
      element.style.fontWeight = '600';
      element.style.textAlign = layer.align || 'center';
      element.style.justifyContent = layer.align === 'left' ? 'flex-start' : layer.align === 'right' ? 'flex-end' : 'center';
      element.style.color = cssColor(layer.color, '#fff');
      element.style.background = String(scalar(layer.background) ?? '').trim().toLowerCase() === 'auto' ? '#0009' : cssColor(layer.background, 'transparent');
      element.style.padding = '8px';
    } else if (kind === 'spinner') {
      if (layer.spinner === 'ring') element.classList.add('spinner-ring');
      else if (layer.spinner === 'dots') { element.classList.add('spinner-dots'); element.textContent = '•••'; }
      else { element.classList.add('placeholder'); element.textContent = layer.spinner === 'hammer' ? '⚒' : String(scalar(layer.spinner) || 'Spinner'); }
    } else {
      element.style.color = cssColor(layer.color, '#fff');
      if (layer.progress === 'ring') {
        element.classList.add('progress-ring');
        element.style.width = Math.max(36, Number(layer.size || 96) * scale) + 'px';
        element.style.height = element.style.width;
      } else if (layer.progress === 'bar') element.classList.add('progress-bar');
      else { element.textContent = '67%'; element.style.fontSize = Math.max(13, Number(layer.size || 28) * scale) + 'px'; }
    }
    screen.append(element);
  }
  if (scalar(layout.mask?.opacity)) {
    const overlay = node('div');
    overlay.style.cssText = 'position:absolute;inset:0;pointer-events:none';
    overlay.style.background = cssColor(layout.mask.color, '#000000');
    overlay.style.opacity = String(layout.mask.opacity);
    screen.append(overlay);
  }
}
