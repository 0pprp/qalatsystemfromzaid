import mapboxgl from 'mapbox-gl'

// Mapbox draws Arabic (right-to-left) labels letter by letter, reversed and unjoined, unless the RTL text plugin is
// loaded. It is loaded once, lazily (only when a map first shows Arabic text). Every dashboard map imports mapboxgl
// from here instead of from 'mapbox-gl'. (v0.3.0 of the plugin fails to load lazily with mapbox-gl 3.1: labels vanish.)
if (mapboxgl.getRTLTextPluginStatus?.() === 'unavailable') {
  mapboxgl.setRTLTextPlugin(
    'https://api.mapbox.com/mapbox-gl-js/plugins/mapbox-gl-rtl-text/v0.2.3/mapbox-gl-rtl-text.js',
    error => { if (error) console.warn('Mapbox RTL text plugin failed to load', error) },
    true,
  )
}

export default mapboxgl
