window.chartInterop = {
    charts: {},
    create: function (canvasId, type, labels, datasets, options) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
        }
        this.charts[canvasId] = new Chart(ctx, {
            type: type,
            data: { labels: labels, datasets: datasets },
            options: options || {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'top' } }
            }
        });
    },
    destroy: function (canvasId) {
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
            delete this.charts[canvasId];
        }
    }
};

// Richer render API accepting a full Chart.js config object. Used by pages
// that want custom scales/legends/tooltips (Price Index, Rental Index,
// Analytics, AI Agent in-chat charts).
window.iretpChart = {
    charts: {},
    render: function (canvasId, cfg) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
        }
        // Patch a few string-based option callbacks the C# side may send.
        try {
            if (cfg && cfg.options && cfg.options.scales) {
                for (const axisName of ['x', 'y']) {
                    const axis = cfg.options.scales[axisName];
                    if (axis && axis.ticks && axis.ticks.callback === 'aed') {
                        axis.ticks.callback = function (v) {
                            if (v >= 1_000_000) return 'AED ' + (v / 1_000_000).toFixed(1) + 'M';
                            if (v >= 1_000)     return 'AED ' + (v / 1_000).toFixed(1) + 'K';
                            return 'AED ' + v;
                        };
                    }
                }
            }
        } catch (e) { /* ignore */ }

        // RFP AI-002 / FR-004 — enforce interactive defaults. AI-generated
        // chart configs often omit options, which leaves Chart.js without
        // hover tooltips. Merge in sensible defaults unless the caller has
        // opted out explicitly.
        cfg = cfg || {};
        cfg.options = cfg.options || {};
        if (cfg.options.responsive === undefined) cfg.options.responsive = true;
        if (cfg.options.maintainAspectRatio === undefined) cfg.options.maintainAspectRatio = false;
        if (cfg.options.interaction === undefined) {
            cfg.options.interaction = { intersect: false, mode: 'nearest', axis: 'x' };
        }
        cfg.options.plugins = cfg.options.plugins || {};
        if (cfg.options.plugins.tooltip === undefined) {
            cfg.options.plugins.tooltip = { enabled: true };
        }
        if (cfg.options.plugins.legend === undefined) {
            cfg.options.plugins.legend = { position: 'top' };
        }
        // Ensure any axis block has a display flag and tick visibility — the
        // RFP's "hover tooltips, zoom, and pan" acceptance needs the axes to
        // render legibly at every density.
        if (cfg.options.scales) {
            for (const ax of Object.values(cfg.options.scales)) {
                if (ax && ax.display === undefined) ax.display = true;
                if (ax && ax.ticks && ax.ticks.display === undefined) ax.ticks.display = true;
            }
        }

        this.charts[canvasId] = new Chart(ctx, cfg);
    },
    destroy: function (canvasId) {
        if (this.charts[canvasId]) {
            this.charts[canvasId].destroy();
            delete this.charts[canvasId];
        }
    }
};

window.downloadFile = function(filename, contentType, byteArray) {
    var blob = new Blob([new Uint8Array(byteArray)], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
