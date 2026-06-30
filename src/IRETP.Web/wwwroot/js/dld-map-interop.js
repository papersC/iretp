window.dldMap = {
    map: null,
    popup: null,
    dotNetRef: null,
    currentAreas: [],
    currentMode: 'projects',
    geoJSON: null,
    selectedFeatureId: null,
    dataMarkers: [],
    animationFrame: null,
    layersAdded: false,
    currentTheme: 'dark',

    themes: {
        dark: {
            tiles: 'dark_all',
            greenTint: '#0a3a1a',
            greenTintOpacity: 0.22,
            fillNoData: '#0a2a14',
            fillHigh: '#22c55e',
            fillMedHigh: '#328B4D',
            fillMed: '#1a5c30',
            fillLow: '#0f3d1f',
            extrusionColor: '#22c55e',
            extrusionOpacity: 0.75,
            labelColor: '#e2ffe2',
            labelHaloColor: '#000000',
            lineSelected: '#4ade80',
            lineHoverData: 'rgba(74, 222, 128, 0.9)',
            lineHoverNoData: 'rgba(74, 222, 128, 0.6)',
            lineData: 'rgba(74, 222, 128, 0.7)',
            lineNoData: 'rgba(74, 222, 128, 0.35)'
        },
        light: {
            tiles: 'light_all',
            greenTint: '#b8e6c8',
            greenTintOpacity: 0.15,
            fillNoData: '#d4e8db',
            fillHigh: '#1D7A3A',
            fillMedHigh: '#328B4D',
            fillMed: '#5BAD73',
            fillLow: '#8FCB9F',
            extrusionColor: '#328B4D',
            extrusionOpacity: 0.65,
            labelColor: '#1A3A24',
            labelHaloColor: '#FFFFFF',
            lineSelected: '#1D7A3A',
            lineHoverData: 'rgba(29, 122, 58, 0.7)',
            lineHoverNoData: 'rgba(100, 140, 110, 0.4)',
            lineData: 'rgba(29, 122, 58, 0.4)',
            lineNoData: 'rgba(100, 140, 110, 0.2)'
        }
    },

    init: function (elementId, dotNetRef) {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        this.dotNetRef = dotNetRef;
        this.layersAdded = false;
        this.selectedFeatureId = null;
        this.dataMarkers = [];
        this.currentTheme = localStorage.getItem('dld-theme') || 'dark';

        var tilePrefix = this.themes[this.currentTheme].tiles;
        this.map = new maplibregl.Map({
            container: elementId,
            style: {
                version: 8,
                glyphs: 'https://fonts.openmaptiles.org/{fontstack}/{range}.pbf',
                sources: {
                    'carto-dark': {
                        type: 'raster',
                        tiles: [
                            'https://a.basemaps.cartocdn.com/' + tilePrefix + '/{z}/{x}/{y}@2x.png',
                            'https://b.basemaps.cartocdn.com/' + tilePrefix + '/{z}/{x}/{y}@2x.png',
                            'https://c.basemaps.cartocdn.com/' + tilePrefix + '/{z}/{x}/{y}@2x.png',
                            'https://d.basemaps.cartocdn.com/' + tilePrefix + '/{z}/{x}/{y}@2x.png'
                        ],
                        tileSize: 256,
                        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/">CARTO</a>'
                    }
                },
                layers: [{
                    id: 'carto-tiles',
                    type: 'raster',
                    source: 'carto-dark',
                    minzoom: 0,
                    maxzoom: 19
                }]
            },
            center: [55.22, 25.08],
            zoom: 11,
            pitch: 45,
            bearing: -17,
            minZoom: 10,
            maxZoom: 17,
            maxBounds: [[54.85, 24.75], [55.65, 25.60]]
        });


        // Navigation control with pitch visualization
        this.map.addControl(new maplibregl.NavigationControl({
            visualizePitch: true,
            showCompass: true
        }), 'bottom-right');

        var self = this;

        // Click empty space to deselect
        this.map.on('click', function (e) {
            var features = self.map.queryRenderedFeatures(e.point, { layers: ['areas-fill'] });
            if (features.length === 0) {
                self.deselectArea();
            }
        });

        return true;
    },

    setMarkers: function (areas, mode) {
        if (!this.map) return;

        this.currentAreas = areas;
        this.currentMode = mode;

        // Clear previous markers
        this.dataMarkers.forEach(function (m) { m.remove(); });
        this.dataMarkers = [];

        // Close popup
        if (this.popup) { this.popup.remove(); this.popup = null; }

        // Deselect previous
        if (this.selectedFeatureId !== null && this.map.getSource('areas')) {
            this.map.setFeatureState(
                { source: 'areas', id: this.selectedFeatureId },
                { selected: false, extrusionHeight: 0 }
            );
        }
        this.selectedFeatureId = null;

        // Build GeoJSON
        this.geoJSON = dubaiAreasGeo.buildFullGeoJSON(areas);

        // Assign stable numeric IDs for feature-state
        this.geoJSON.features.forEach(function (f, i) { f.id = i; });

        var self = this;

        if (this.map.isStyleLoaded()) {
            this._applyData(areas, mode);
        } else {
            this.map.on('load', function () {
                self._applyData(areas, mode);
            });
        }
    },

    _applyData: function (areas, mode) {
        var self = this;

        if (this.map.getSource('areas')) {
            this.map.getSource('areas').setData(this.geoJSON);
        } else {
            this.map.addSource('areas', {
                type: 'geojson',
                data: this.geoJSON
            });
        }

        if (!this.layersAdded) {
            this._addLayers();
            this.layersAdded = true;
        }

        // Update fill colors for current mode
        this.map.setPaintProperty('areas-fill', 'fill-color', this._getFillColorExpression(mode));

        // Project-pin layer visibility is driven by the active mode. In
        // `projects` mode we show the pin layer (setProjectPins must have been
        // called at least once to populate the source); in any other mode we
        // hide it so it doesn't double up with the area heatmap.
        if (this.map.getLayer('project-pin')) {
            this.map.setLayoutProperty('project-pin', 'visibility',
                mode === 'projects' ? 'visible' : 'none');
        }

        // Add data markers
        areas.forEach(function (a) { self.addDataMarker(a, mode); });

        // Fit bounds
        if (this.geoJSON.features.length > 0) {
            var bounds = new maplibregl.LngLatBounds();
            this.geoJSON.features.forEach(function (f) {
                if (f.geometry && f.geometry.type === 'Polygon') {
                    f.geometry.coordinates[0].forEach(function (c) { bounds.extend(c); });
                }
            });
            if (!bounds.isEmpty()) {
                this.map.fitBounds(bounds, { padding: 40, maxZoom: 13, pitch: 45, bearing: -17 });
            }
        }
    },

    _addLayers: function () {
        var self = this;

        // Green tint overlay above tiles but below data
        if (!this.map.getSource('green-overlay')) {
            this.map.addSource('green-overlay', {
                type: 'geojson',
                data: { type: 'FeatureCollection', features: [{
                    type: 'Feature',
                    geometry: { type: 'Polygon', coordinates: [[[54, 24], [56, 24], [56, 26], [54, 26], [54, 24]]] }
                }]}
            });
            this.map.addLayer({
                id: 'green-tint',
                type: 'fill',
                source: 'green-overlay',
                paint: {
                    'fill-color': this.themes[this.currentTheme].greenTint,
                    'fill-opacity': this.themes[this.currentTheme].greenTintOpacity
                }
            });
        }

        // Flat fill layer
        this.map.addLayer({
            id: 'areas-fill',
            type: 'fill',
            source: 'areas',
            paint: {
                'fill-color': this._getFillColorExpression(this.currentMode),
                'fill-opacity': [
                    'case',
                    ['boolean', ['feature-state', 'hover'], false],
                        ['case', ['boolean', ['get', 'hasData'], false], 0.25, 0.18],
                    ['boolean', ['get', 'hasData'], false], 0.15,
                    0.12
                ]
            }
        });

        // Outline layer
        this.map.addLayer({
            id: 'areas-line',
            type: 'line',
            source: 'areas',
            paint: {
                'line-color': this._getLineColorExpression(),
                'line-width': [
                    'case',
                    ['boolean', ['feature-state', 'selected'], false], 3,
                    ['boolean', ['feature-state', 'hover'], false],
                        ['case', ['boolean', ['get', 'hasData'], false], 2.5, 2],
                    ['boolean', ['get', 'hasData'], false], 1.8,
                    1.2
                ]
            }
        });

        // Selected area source + 3D extrusion layer (only renders the clicked polygon)
        this.map.addSource('selected-area', {
            type: 'geojson',
            data: { type: 'FeatureCollection', features: [] }
        });
        this.map.addLayer({
            id: 'areas-3d',
            type: 'fill-extrusion',
            source: 'selected-area',
            paint: {
                'fill-extrusion-color': this.themes[this.currentTheme].extrusionColor,
                'fill-extrusion-height': 0,
                'fill-extrusion-base': 0,
                'fill-extrusion-opacity': this.themes[this.currentTheme].extrusionOpacity
            }
        });

        // Area name labels (symbol layer on polygon centroids)
        this.map.addLayer({
            id: 'area-labels',
            type: 'symbol',
            source: 'areas',
            layout: {
                'text-field': ['get', 'name'],
                'text-size': ['interpolate', ['linear'], ['zoom'], 10, 9, 12, 12, 14, 15, 16, 17],
                'text-font': ['Open Sans Bold'],
                'text-transform': 'uppercase',
                'text-letter-spacing': 0.05,
                'symbol-placement': 'point',
                'text-allow-overlap': false,
                'text-ignore-placement': false,
                'text-optional': true,
                'text-padding': 8,
                'symbol-sort-key': ['case', ['boolean', ['get', 'hasData'], false], ['*', -1, ['to-number', ['get', 'transactionCount'], 0]], 9999],
                'text-pitch-alignment': 'viewport',
                'text-rotation-alignment': 'viewport',
                'text-max-width': 8
            },
            paint: {
                'text-color': this.themes[this.currentTheme].labelColor,
                'text-halo-color': this.themes[this.currentTheme].labelHaloColor,
                'text-halo-width': 2.5,
                'text-halo-blur': 0.5,
                'text-opacity': ['interpolate', ['linear'], ['zoom'], 10, 0.7, 13, 1]
            }
        });

        // Hover interaction with tooltip
        var hoveredId = null;
        var hoverTooltip = null;

        this.map.on('mousemove', 'areas-fill', function (e) {
            if (e.features.length > 0) {
                if (hoveredId !== null && hoveredId !== self.selectedFeatureId) {
                    self.map.setFeatureState({ source: 'areas', id: hoveredId }, { hover: false });
                }
                hoveredId = e.features[0].id;
                if (hoveredId !== self.selectedFeatureId) {
                    self.map.setFeatureState({ source: 'areas', id: hoveredId }, { hover: true });
                }
                self.map.getCanvas().style.cursor = 'pointer';

                // Show hover tooltip
                var p = e.features[0].properties;
                var tipHtml = '<div class="hover-tip"><strong>' + p.name + '</strong>';
                if (p.hasData === true || p.hasData === 'true') {
                    tipHtml += '<span class="hover-tip-stats">' + Number(p.transactionCount).toLocaleString() + ' txns &middot; AED ' + self.shortNum(Number(p.totalValue)) + '</span>';
                }
                tipHtml += '</div>';

                if (!hoverTooltip) {
                    hoverTooltip = new maplibregl.Popup({ closeButton: false, closeOnClick: false, className: 'hover-tooltip-wrapper', offset: [0, -12] });
                }
                hoverTooltip.setLngLat(e.lngLat).setHTML(tipHtml).addTo(self.map);
            }
        });

        this.map.on('mouseleave', 'areas-fill', function () {
            if (hoveredId !== null && hoveredId !== self.selectedFeatureId) {
                self.map.setFeatureState({ source: 'areas', id: hoveredId }, { hover: false });
            }
            hoveredId = null;
            self.map.getCanvas().style.cursor = '';
            if (hoverTooltip) { hoverTooltip.remove(); hoverTooltip = null; }
        });

        // Click on polygon
        this.map.on('click', 'areas-fill', function (e) {
            if (e.features.length > 0) {
                var clickedFeature = e.features[0];
                // Use original GeoJSON feature for full geometry
                var original = self.geoJSON.features.find(function (f) { return f.id === clickedFeature.id; });
                if (original) {
                    self.selectAreaPolygon(original);
                }
            }
        });
    },

    _getFillColorExpression: function (mode) {
        var t = this.themes[this.currentTheme];
        if (mode === 'pricesqft') {
            return [
                'case',
                ['!', ['boolean', ['get', 'hasData'], false]], t.fillNoData,
                ['>', ['to-number', ['get', 'avgPricePerSqft'], 0], 2500], t.fillHigh,
                ['>', ['to-number', ['get', 'avgPricePerSqft'], 0], 1800], t.fillMedHigh,
                ['>', ['to-number', ['get', 'avgPricePerSqft'], 0], 1200], t.fillMed,
                t.fillLow
            ];
        } else if (mode === 'volume') {
            return [
                'case',
                ['!', ['boolean', ['get', 'hasData'], false]], t.fillNoData,
                ['>', ['to-number', ['get', 'totalValue'], 0], 8e9], t.fillHigh,
                ['>', ['to-number', ['get', 'totalValue'], 0], 4e9], t.fillMedHigh,
                ['>', ['to-number', ['get', 'totalValue'], 0], 2e9], t.fillMed,
                t.fillLow
            ];
        } else if (mode === 'yield') {
            return [
                'case',
                ['!', ['boolean', ['get', 'hasData'], false]], t.fillNoData,
                ['>', ['to-number', ['get', 'rentalYield'], 0], 8], t.fillHigh,
                ['>', ['to-number', ['get', 'rentalYield'], 0], 6], t.fillMedHigh,
                ['>', ['to-number', ['get', 'rentalYield'], 0], 4], t.fillMed,
                t.fillLow
            ];
        } else if (mode === 'esg') {
            // ESG certified-green-building coverage (RFP §20) — zones with no
            // projects in the registry stay neutral so the map doesn't imply
            // "0% certified" when we actually have no data.
            return [
                'case',
                ['!', ['boolean', ['get', 'hasData'], false]], t.fillNoData,
                ['>', ['to-number', ['get', 'esgCoverage'], 0], 60], t.fillHigh,
                ['>', ['to-number', ['get', 'esgCoverage'], 0], 35], t.fillMedHigh,
                ['>', ['to-number', ['get', 'esgCoverage'], 0], 15], t.fillMed,
                t.fillLow
            ];
        } else {
            return [
                'case',
                ['!', ['boolean', ['get', 'hasData'], false]], t.fillNoData,
                ['>', ['to-number', ['get', 'transactionCount'], 0], 800], t.fillHigh,
                ['>', ['to-number', ['get', 'transactionCount'], 0], 400], t.fillMedHigh,
                ['>', ['to-number', ['get', 'transactionCount'], 0], 200], t.fillMed,
                t.fillLow
            ];
        }
    },

    _getLineColorExpression: function () {
        var t = this.themes[this.currentTheme];
        return [
            'case',
            ['boolean', ['feature-state', 'selected'], false], t.lineSelected,
            ['boolean', ['feature-state', 'hover'], false],
                ['case', ['boolean', ['get', 'hasData'], false], t.lineHoverData, t.lineHoverNoData],
            ['boolean', ['get', 'hasData'], false], t.lineData,
            t.lineNoData
        ];
    },

    setTheme: function (theme) {
        this.currentTheme = theme;
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('dld-theme', theme);

        if (!this.map || !this.map.isStyleLoaded()) return;

        var t = this.themes[theme];

        // Swap base tiles
        var source = this.map.getSource('carto-dark');
        if (source) {
            source.setTiles([
                'https://a.basemaps.cartocdn.com/' + t.tiles + '/{z}/{x}/{y}@2x.png',
                'https://b.basemaps.cartocdn.com/' + t.tiles + '/{z}/{x}/{y}@2x.png',
                'https://c.basemaps.cartocdn.com/' + t.tiles + '/{z}/{x}/{y}@2x.png',
                'https://d.basemaps.cartocdn.com/' + t.tiles + '/{z}/{x}/{y}@2x.png'
            ]);
        }

        // Update green tint overlay
        if (this.map.getLayer('green-tint')) {
            this.map.setPaintProperty('green-tint', 'fill-color', t.greenTint);
            this.map.setPaintProperty('green-tint', 'fill-opacity', t.greenTintOpacity);
        }

        // Update fill colors
        if (this.map.getLayer('areas-fill')) {
            this.map.setPaintProperty('areas-fill', 'fill-color', this._getFillColorExpression(this.currentMode));
        }

        // Update outline colors
        if (this.map.getLayer('areas-line')) {
            this.map.setPaintProperty('areas-line', 'line-color', this._getLineColorExpression());
        }

        // Update extrusion (dedicated source, simple color)
        if (this.map.getLayer('areas-3d')) {
            this.map.setPaintProperty('areas-3d', 'fill-extrusion-color', t.extrusionColor);
            this.map.setPaintProperty('areas-3d', 'fill-extrusion-opacity', t.extrusionOpacity);
        }

        // Update labels
        if (this.map.getLayer('area-labels')) {
            this.map.setPaintProperty('area-labels', 'text-color', t.labelColor);
            this.map.setPaintProperty('area-labels', 'text-halo-color', t.labelHaloColor);
        }

        // Refresh markers (CSS classes auto-update via data-theme but need DOM refresh)
        if (this.currentAreas.length > 0) {
            this.dataMarkers.forEach(function (m) { m.remove(); });
            this.dataMarkers = [];
            var self = this;
            this.currentAreas.forEach(function (a) { self.addDataMarker(a, self.currentMode); });
        }
    },

    selectAreaPolygon: function (feature) {
        var props = feature.properties;

        // Deselect previous
        if (this.selectedFeatureId !== null) {
            var prevId = this.selectedFeatureId;
            this.map.setFeatureState(
                { source: 'areas', id: prevId },
                { selected: false }
            );
            // Clear extrusion immediately (new selection replaces it)
            this.map.getSource('selected-area').setData({
                type: 'FeatureCollection', features: []
            });
            this.map.setPaintProperty('areas-3d', 'fill-extrusion-height', 0);
        }

        // Select new
        this.selectedFeatureId = feature.id;
        this.map.setFeatureState(
            { source: 'areas', id: feature.id },
            { selected: true }
        );

        // Set selected polygon geometry into dedicated source and animate rise
        this.map.getSource('selected-area').setData({
            type: 'FeatureCollection',
            features: [{ type: 'Feature', geometry: feature.geometry, properties: {} }]
        });
        this._animateSelectedExtrusion(200, 500);

        // Close existing popup
        if (this.popup) this.popup.remove();

        // Calculate popup position (top center of polygon bounding box)
        var lngLat = this._getPopupPosition(feature);

        // Show popup
        if (props.hasData) {
            this._showStatsPopup(props, lngLat);
        } else {
            this._showNoDataPopup(props, lngLat);
        }

        // Notify Blazor
        if (this.dotNetRef && props.hasData) {
            this.dotNetRef.invokeMethodAsync('OnAreaSelected',
                props.areaId,
                props.name,
                props.transactionCount,
                props.totalValue,
                props.avgPricePerSqft
            );
        }
    },

    selectAreaByData: function (area) {
        if (!this.geoJSON) return;
        var feature = this.geoJSON.features.find(function (f) {
            return f.properties.areaId === area.areaId;
        });
        if (feature) {
            this.selectAreaPolygon(feature);
        }
    },

    deselectArea: function () {
        if (this.selectedFeatureId !== null) {
            var id = this.selectedFeatureId;
            var self = this;

            // Animate extrusion down then clear
            this._animateSelectedExtrusion(0, 400);

            setTimeout(function () {
                self.map.setFeatureState(
                    { source: 'areas', id: id },
                    { selected: false }
                );
                self.map.getSource('selected-area').setData({
                    type: 'FeatureCollection', features: []
                });
            }, 420);

            this.selectedFeatureId = null;
        }

        if (this.popup) { this.popup.remove(); this.popup = null; }

        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnAreaDeselected');
        }
    },

    _animateSelectedExtrusion: function (targetHeight, duration) {
        var self = this;
        var startTime = performance.now();
        var currentHeight = this._selectedExtrusionHeight || 0;

        if (this.animationFrame) cancelAnimationFrame(this.animationFrame);

        function animate(time) {
            var progress = Math.min((time - startTime) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            var height = currentHeight + (targetHeight - currentHeight) * eased;
            // Clamp: MapLibre rejects negative fill-extrusion-height (easing can overshoot by fp-epsilon)
            if (height < 0) height = 0;

            self._selectedExtrusionHeight = height;
            self.map.setPaintProperty('areas-3d', 'fill-extrusion-height', height);

            if (progress < 1) {
                self.animationFrame = requestAnimationFrame(animate);
            } else {
                self.animationFrame = null;
            }
        }

        this.animationFrame = requestAnimationFrame(animate);
    },

    _getPopupPosition: function (feature) {
        var coords = feature.geometry.coordinates[0];
        var maxLat = -Infinity;
        var sumLng = 0;
        var count = 0;
        coords.forEach(function (c) {
            if (c[1] > maxLat) maxLat = c[1];
            sumLng += c[0];
            count++;
        });
        return [sumLng / count, maxLat];
    },

    _showStatsPopup: function (props, lngLat) {
        var self = this;
        var offPlanPct = props.offPlanPercent != null ? Math.round(props.offPlanPercent) : 58;
        var readyPct = 100 - offPlanPct;

        var content =
            '<div class="stats-popup">' +
            '<div class="sp-header">' +
            '<div class="sp-name">' + props.name + '</div>' +
            '<div class="sp-badge">' + this._formatModeName(this.currentMode) + '</div>' +
            '</div>' +
            '<div class="sp-divider"></div>' +
            '<div class="sp-grid">' +
            '<div class="sp-stat">' +
            '<div class="sp-stat-icon txn">&#9733;</div>' +
            '<div class="sp-stat-info">' +
            '<span class="sp-stat-val">' + props.transactionCount.toLocaleString() + '</span>' +
            '<span class="sp-stat-lbl">Transactions</span>' +
            '</div></div>' +
            '<div class="sp-stat">' +
            '<div class="sp-stat-icon vol">&#9650;</div>' +
            '<div class="sp-stat-info">' +
            '<span class="sp-stat-val">AED ' + self.formatNumber(props.totalValue) + '</span>' +
            '<span class="sp-stat-lbl">Total Volume</span>' +
            '</div></div>' +
            '<div class="sp-stat">' +
            '<div class="sp-stat-icon psf">&#9632;</div>' +
            '<div class="sp-stat-info">' +
            '<span class="sp-stat-val">AED ' + Math.round(props.avgPricePerSqft).toLocaleString() + '</span>' +
            '<span class="sp-stat-lbl">Avg Price/Sqft</span>' +
            '</div></div>' +
            '<div class="sp-stat">' +
            '<div class="sp-stat-icon med">&#9679;</div>' +
            '<div class="sp-stat-info">' +
            '<span class="sp-stat-val">AED ' + self.formatNumber(props.totalValue / Math.max(props.transactionCount, 1)) + '</span>' +
            '<span class="sp-stat-lbl">Avg Deal Size</span>' +
            '</div></div>' +
            '</div>' +
            '<div class="sp-divider"></div>' +
            '<div class="sp-bars">' +
            '<div class="sp-bar-row"><span class="sp-bar-lbl">Off-Plan</span><div class="sp-bar-track"><div class="sp-bar-fill offplan" style="width:' + offPlanPct + '%"></div></div><span class="sp-bar-pct">' + offPlanPct + '%</span></div>' +
            '<div class="sp-bar-row"><span class="sp-bar-lbl">Ready</span><div class="sp-bar-track"><div class="sp-bar-fill ready" style="width:' + readyPct + '%"></div></div><span class="sp-bar-pct">' + readyPct + '%</span></div>' +
            '</div>' +
            '</div>';

        this.popup = new maplibregl.Popup({
            closeButton: true,
            closeOnClick: false,
            maxWidth: '320px',
            className: 'stats-popup-wrapper',
            offset: [0, -10]
        })
        .setLngLat(lngLat)
        .setHTML(content)
        .addTo(this.map);
    },

    _showNoDataPopup: function (props, lngLat) {
        var content = '<div class="stats-popup"><div class="sp-header"><div class="sp-name">' + props.name + '</div></div>' +
            '<div class="sp-divider"></div><p class="sp-stat-lbl" style="font-size:13px;margin:0;">No transaction data available for this area.</p></div>';

        this.popup = new maplibregl.Popup({
            closeButton: true,
            closeOnClick: false,
            maxWidth: '280px',
            className: 'stats-popup-wrapper',
            offset: [0, -10]
        })
        .setLngLat(lngLat)
        .setHTML(content)
        .addTo(this.map);
    },

    _formatModeName: function (mode) {
        switch (mode) {
            case 'pricesqft': return 'Price/Sqft';
            case 'projects': return 'Projects';
            case 'volume': return 'Volume';
            case 'yield': return 'Rental Yield';
            case 'esg': return 'ESG Coverage';
            default: return mode;
        }
    },

    // ------------------------------------------------------------------
    // FR-011 — individual project pins with MapLibre native clustering.
    //   source: pins collection with cluster: true, clusterMaxZoom: 14.
    //   layers: cluster-circles (sized/colored by point_count),
    //           cluster-count (text), pin-circles (coloured by status).
    //   click: cluster → getClusterExpansionZoom → flyTo;
    //          pin     → popup with name/developer/status/completion.
    // ------------------------------------------------------------------
    projectPinsAdded: false,
    projectStatusColor: {
        'Completed':         '#22c55e', // green
        'UnderConstruction': '#F1B44C', // amber
        'FutureAnnounced':   '#50A5F1', // blue
        'Stalled':           '#EF4444'  // red
    },

    setProjectPins: function (pins) {
        if (!this.map) return;
        var self = this;
        var apply = function () {
            var data = {
                type: 'FeatureCollection',
                features: (pins || []).map(function (p) {
                    return {
                        type: 'Feature',
                        geometry: { type: 'Point', coordinates: [p.longitude, p.latitude] },
                        properties: {
                            id: p.id, name: p.name, developer: p.developer,
                            zone: p.zone, status: p.status,
                            completion: p.completionPercent, units: p.totalUnits
                        }
                    };
                })
            };

            // Clean up any prior state (including legacy cluster layers from
            // earlier iterations that used MapLibre's native clustering).
            ['project-pin', 'project-clusters', 'project-cluster-count'].forEach(function (id) {
                if (self.map.getLayer(id)) self.map.removeLayer(id);
            });
            if (self.map.getSource('project-pins')) self.map.removeSource('project-pins');

            // Non-clustered GeoJSON source. MapLibre's supercluster worker
            // stalls on large sources during the first-load race on this
            // style, so pins are rendered individually and shrink at low
            // zoom via an interpolate expression — the density visual
            // falls out of overlap rather than explicit aggregation.
            self.map.addSource('project-pins', { type: 'geojson', data: data });
            self.map.addLayer({
                id: 'project-pin',
                type: 'circle',
                source: 'project-pins',
                paint: {
                    'circle-color': [
                        'match', ['get', 'status'],
                        'Completed',         self.projectStatusColor.Completed,
                        'UnderConstruction', self.projectStatusColor.UnderConstruction,
                        'FutureAnnounced',   self.projectStatusColor.FutureAnnounced,
                        'Stalled',           self.projectStatusColor.Stalled,
                        '#cccccc'
                    ],
                    'circle-radius': [
                        'interpolate', ['linear'], ['zoom'],
                        9,  3,
                        11, 5,
                        14, 8,
                        17, 11
                    ],
                    'circle-stroke-color': '#ffffff',
                    'circle-stroke-width': 1.2,
                    'circle-opacity': 0.9
                }
            });

            // Click/hover handlers are a one-time install — MapLibre looks
            // features up by the current layer name, so re-registering on
            // every apply would stack up duplicates.
            if (!self.projectPinsAdded) {
                self.map.on('click', 'project-pin', function (e) {
                    var f = e.features[0];
                    var p = f.properties;
                    if (self.popup) self.popup.remove();
                    var statusBadge = '<span style="display:inline-block;padding:2px 8px;border-radius:4px;color:#fff;font-size:11px;background:' +
                        (self.projectStatusColor[p.status] || '#888') + '">' + p.status + '</span>';
                    self.popup = new maplibregl.Popup({ closeButton: true, offset: 12 })
                        .setLngLat(f.geometry.coordinates)
                        .setHTML(
                            '<div style="font-weight:600;margin-bottom:4px;">' + p.name + '</div>' +
                            '<div style="font-size:12px;color:#555;">' + p.developer + ' · ' + p.zone + '</div>' +
                            '<div style="margin-top:6px;">' + statusBadge + ' &nbsp;' + p.completion + '% · ' + p.units + ' units</div>')
                        .addTo(self.map);
                });
                self.map.on('mouseenter', 'project-pin', function () { self.map.getCanvas().style.cursor = 'pointer'; });
                self.map.on('mouseleave', 'project-pin', function () { self.map.getCanvas().style.cursor = ''; });
                self.projectPinsAdded = true;
            }

            self.map.setLayoutProperty('project-pin', 'visibility', 'visible');
        };

        if (this.map.isStyleLoaded()) apply();
        else this.map.once('load', apply);
    },

    clearProjectPins: function () {
        if (!this.map) return;
        var self = this;
        ['project-pin', 'project-clusters', 'project-cluster-count'].forEach(function (id) {
            if (self.map.getLayer(id)) self.map.setLayoutProperty(id, 'visibility', 'none');
        });
        if (this.popup) { this.popup.remove(); this.popup = null; }
    },

    addDataMarker: function (area, mode) {
        // Projects mode renders individual clustered pins via setProjectPins
        // (FR-011). Per-area aggregate circles would double-layer on top.
        if (mode === 'projects') return;

        var self = this;
        var el = document.createElement('div');
        el.className = 'marker-data';

        if (mode === 'pricesqft') {
            el.innerHTML = '<div class="m-price-tag">AED ' + Math.round(area.avgPricePerSqft).toLocaleString() + '</div>';
        } else if (mode === 'esg') {
            var pct = Number(area.esgCoveragePct != null ? area.esgCoveragePct : 0);
            el.innerHTML = '<div class="m-esg-tag" title="Certified green-building coverage">&#127807; ' + pct.toFixed(0) + '%</div>';
        } else {
            var sz = Math.max(24, Math.min(48, Math.sqrt(area.totalValue / 1e7) * 2.5));
            el.innerHTML = '<div class="m-circle-green" style="width:' + sz + 'px;height:' + sz + 'px;">' +
                '<span class="m-num">' + self.shortNum(area.totalValue) + '</span></div>';
        }

        el.addEventListener('click', function (e) {
            e.stopPropagation();
            self.selectAreaByData(area);
        });

        var marker = new maplibregl.Marker({ element: el, anchor: 'center' })
            .setLngLat([area.longitude, area.latitude])
            .addTo(this.map);

        this.dataMarkers.push(marker);
    },

    shortNum: function (val) {
        if (val >= 1e9) return (val / 1e9).toFixed(1) + 'B';
        if (val >= 1e6) return (val / 1e6).toFixed(0) + 'M';
        if (val >= 1e3) return (val / 1e3).toFixed(0) + 'K';
        return val.toString();
    },

    formatNumber: function (val) {
        if (val >= 1e9) return (val / 1e9).toFixed(2) + 'B';
        if (val >= 1e6) return (val / 1e6).toFixed(1) + 'M';
        if (val >= 1e3) return (val / 1e3).toFixed(0) + 'K';
        return Math.round(val).toLocaleString();
    },

    resize: function () {
        if (this.map) this.map.resize();
    },

    // Jump camera to a specific area by id (used by search box)
    jumpToArea: function (areaId) {
        if (!this.geoJSON) return;
        var feature = this.geoJSON.features.find(function (f) {
            return f.properties.areaId === areaId;
        });
        if (!feature || !this.map) return;
        this.selectAreaPolygon(feature);
        var lng = feature.properties.longitude || feature.properties.lng;
        var lat = feature.properties.latitude || feature.properties.lat;
        if (lng && lat) {
            this.map.flyTo({ center: [lng, lat], zoom: 13, pitch: 55, bearing: -17, duration: 1200 });
        }
    },

    // Recenter to Dubai-wide bounds
    recenter: function () {
        if (!this.map) return;
        if (this.geoJSON && this.geoJSON.features.length > 0) {
            var bounds = new maplibregl.LngLatBounds();
            this.geoJSON.features.forEach(function (f) {
                if (f.geometry && f.geometry.type === 'Polygon') {
                    f.geometry.coordinates[0].forEach(function (c) { bounds.extend(c); });
                }
            });
            if (!bounds.isEmpty()) {
                this.map.fitBounds(bounds, { padding: 60, maxZoom: 12, pitch: 45, bearing: -17, duration: 800 });
                return;
            }
        }
        this.map.flyTo({ center: [55.22, 25.08], zoom: 11, pitch: 45, bearing: -17, duration: 800 });
    },

    // Toggle fullscreen on the map container element
    toggleFullscreen: function (elementId) {
        var el = document.getElementById(elementId);
        if (!el) return;
        if (document.fullscreenElement) {
            document.exitFullscreen();
        } else if (el.requestFullscreen) {
            el.requestFullscreen();
        }
    },

    destroy: function () {
        if (this.animationFrame) cancelAnimationFrame(this.animationFrame);
        if (this.popup) { this.popup.remove(); this.popup = null; }
        this.dataMarkers.forEach(function (m) { m.remove(); });
        this.dataMarkers = [];
        if (this.map) { this.map.remove(); this.map = null; }
        this.dotNetRef = null;
        this.layersAdded = false;
    }
};
