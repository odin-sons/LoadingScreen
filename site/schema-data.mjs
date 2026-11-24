const cache = new WeakMap();

export function schemaOptions(schema) {
  if (cache.has(schema)) return cache.get(schema);
  const defs = schema.$defs;
  const names = (name) => defs[name].enum;
  const keys = (properties) => Object.keys(properties).map((name) => name.toLowerCase());
  const fields = (name) => defs[name].properties;
  const section = (name) => Object.fromEntries(Object.entries(fields(name)).map(([key, field]) =>
    [field['x-modelProperty'] ?? key, field['x-parserType']]));

  const options = {
    biomes: names('Biome'),
    deathCauses: names('DeathCause'),
    animations: names('AnimationPreset'),
    anchors: names('AnchorName'),
    globalKeys: names('GlobalKey'),
    conditions: names('ConditionName'),
    languages: defs.Language['x-nameToCode'],
    imageExtensions: names('ImageExtension'),
    audioExtensions: names('AudioExtension'),
    layerKinds: defs.Layer['x-layerKinds'],
    maxLayers: fields('Layout').layers.maxItems,
    enums: Object.fromEntries([
      ['anchor', 'AnchorValues'],
      ['fitMode', 'FitModeValues'],
      ['sides', 'SidesValues'],
      ['fadeCurve', 'FadeCurveValues'],
      ['progressStyle', 'ProgressStyleValues'],
      ['textAlign', 'TextAlignValues'],
      ['conditionKind', 'ConditionKindValues']
    ].map(([key, name]) => [key, defs[name]['x-enumValues']])),
    defaultSections: {
      tip: section('TipDefaults'),
      continuePrompt: section('ContinuePromptDefaults'),
      separator: section('SeparatorDefaults'),
      spinner: section('SpinnerDefaults')
    },
    keys: {
      root: keys(schema.properties),
      defaults: keys(fields('ScreenDefaults')),
      layer: keys(fields('Layer')),
      layout: keys(fields('Layout')),
      entry: keys(fields('Entry')),
      list: keys(fields('EntryList')),
      placement: keys(fields('Placement')),
      animatedPlacement: keys(fields('AnimatedPlacement')),
      fit: keys(fields('Fit')),
      position: keys(fields('Position')),
      fade: keys(fields('Fade')),
      animation: keys(fields('AnimationSpec')),
      vignette: keys(fields('VignetteSettings')),
      mask: keys(fields('MaskSettings')),
      zoom: keys(fields('LegacyZoom')),
      when: keys(fields('When'))
    }
  };
  cache.set(schema, options);
  return options;
}
