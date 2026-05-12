// ── FlowChart Editor — JS Interop ────────────────────────────────────────────

var _blazorCanvasRef = null;

window.downloadFile = function(fileName, mimeType, content) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = fileName;
    document.body.appendChild(a); a.click();
    document.body.removeChild(a); URL.revokeObjectURL(url);
};

window.triggerClick = function(id) {
    const el = document.getElementById(id);
    if (el) el.click();
};

window.focusElement = function(id) {
    setTimeout(() => {
        const el = document.getElementById(id);
        if (el) { el.focus(); el.select(); }
    }, 50);
};

window.exportCanvasSvg = function(svgId, fileName) {
    const svg = document.getElementById(svgId);
    if (!svg) return;
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    const svgStr = new XMLSerializer().serializeToString(clone);
    const blob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = fileName;
    document.body.appendChild(a); a.click();
    document.body.removeChild(a); URL.revokeObjectURL(url);
};

window.exportCanvasPng = function(svgId, fileName, scale) {
    const svg = document.getElementById(svgId);
    if (!svg) return;
    scale = scale || 2;
    const w = parseFloat(svg.getAttribute('width')  || svg.viewBox.baseVal.width);
    const h = parseFloat(svg.getAttribute('height') || svg.viewBox.baseVal.height);
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', w); bg.setAttribute('height', h);
    bg.setAttribute('fill', '#f1f5f9');
    clone.insertBefore(bg, clone.firstChild);
    const svgStr  = new XMLSerializer().serializeToString(clone);
    const svgBlob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
    const svgUrl  = URL.createObjectURL(svgBlob);
    const img = new Image();
    img.onload = function() {
        const canvas = document.createElement('canvas');
        canvas.width = w * scale; canvas.height = h * scale;
        const ctx = canvas.getContext('2d');
        ctx.scale(scale, scale); ctx.drawImage(img, 0, 0);
        URL.revokeObjectURL(svgUrl);
        canvas.toBlob(function(blob) {
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url; a.download = fileName;
            document.body.appendChild(a); a.click();
            document.body.removeChild(a); URL.revokeObjectURL(url);
        }, 'image/png');
    };
    img.src = svgUrl;
};

window.registerCanvasComponent = function(dotNetRef) {
    _blazorCanvasRef = dotNetRef;

    // Global mouseup — catches endpoint drops anywhere on screen
    document.addEventListener('mouseup', function(e) {
        if (_blazorCanvasRef) {
            var svg = document.getElementById('fc-svg-canvas');
            var rect = svg ? svg.getBoundingClientRect() : { left: 0, top: 0 };
            _blazorCanvasRef.invokeMethodAsync('OnGlobalMouseUp',
                e.clientX, e.clientY, rect.left, rect.top);
        }
    });
};

window.unregisterCanvasComponent = function() {
    _blazorCanvasRef = null;
};

document.addEventListener('click', function(e) {
    if (_blazorCanvasRef && !e.target.closest('.fc-file-menu-wrap')) {
        _blazorCanvasRef.invokeMethodAsync('CloseFileMenu');
    }
});

document.addEventListener('dblclick', function(e) {
    if (!_blazorCanvasRef) return;
    var el = e.target;
    var connId = null;
    for (var i = 0; i < 5 && el; i++) {
        connId = el.getAttribute && el.getAttribute('data-conn-id');
        if (connId) break;
        el = el.parentElement;
    }
    if (connId) {
        e.stopPropagation();
        e.preventDefault();
        _blazorCanvasRef.invokeMethodAsync('OnConnectionDblClick', connId);
    }
}, true);
