window.mapInterop = {
    map: null,
    markers: [],
    init: function (elementId, lat, lng, zoom) {
        if (this.map) { this.map.remove(); this.map = null; }
        this.map = L.map(elementId).setView([lat, lng], zoom);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 18
        }).addTo(this.map);
    },
    addZoneMarkers: function (zones, dotNetRef) {
        if (!this.map) return;
        zones.forEach(function (z) {
            if (z.centerLat && z.centerLng) {
                var color = z.transactionCount > 55 ? '#d32f2f' : z.transactionCount > 45 ? '#f57c00' : '#388e3c';
                var circle = L.circleMarker([z.centerLat, z.centerLng], {
                    radius: Math.max(8, Math.min(20, z.transactionCount / 4)),
                    fillColor: color,
                    color: '#fff',
                    weight: 2,
                    opacity: 1,
                    fillOpacity: 0.7
                }).addTo(window.mapInterop.map);
                circle.bindTooltip(z.name + '<br/>Txns: ' + z.transactionCount + '<br/>Avg: ' + Math.round(z.avgPricePerSqft) + ' AED/sqft');
                circle.on('click', function () {
                    dotNetRef.invokeMethodAsync('OnZoneClicked', z.zoneId);
                });
            }
        });
    },
    addProjectMarkers: function (projects) {
        if (!this.map) return;
        this.markers.forEach(function (m) { m.remove(); });
        this.markers = [];
        projects.forEach(function (p) {
            if (p.latitude && p.longitude) {
                var statusColors = { 1: '#4caf50', 2: '#ff9800', 3: '#f44336', 4: '#2196f3' };
                var marker = L.marker([p.latitude, p.longitude]).addTo(window.mapInterop.map);
                marker.bindPopup('<b>' + p.name + '</b><br/>' + (p.developerName || '') +
                    '<br/>Status: ' + p.status + '<br/>Completion: ' + p.completionPercentage + '%');
                window.mapInterop.markers.push(marker);
            }
        });
    },
    destroy: function () {
        if (this.map) { this.map.remove(); this.map = null; }
        this.markers = [];
    }
};
