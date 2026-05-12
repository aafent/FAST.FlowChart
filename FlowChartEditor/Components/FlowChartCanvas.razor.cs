using FlowChartEditor.Models;
using FlowChartEditor.Models.Artifacts;
using FlowChartEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Globalization;

namespace FlowChartEditor.Components;

public partial class FlowChartCanvas : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Zoom ──────────────────────────────────────────────────────────────────
    private double _zoom = 1.0;
    private const double ZoomMin = 0.25;
    private const double ZoomMax = 3.0;
    private const double ZoomStep = 0.25;
    private bool _snapToGrid = true;

    // ── Placement mode ────────────────────────────────────────────────────────
    private ArtifactType? _activePlacementType;
    private string?        _activeUfaName;       // set when placing a UFA
    private PointF?        _ghostPoint;

    // ── Artifact dragging ─────────────────────────────────────────────────────
    private bool _isDraggingArtifact;
    private bool _dragMoved;
    private PointF _dragStartCanvas;
    private Dictionary<Guid, (double X, double Y)> _dragStartPositions = new();

    // ── Rubber-band selection ─────────────────────────────────────────────────
    private bool _isRubberBanding;
    private PointF _rubberBandStart;
    private RectF? _rubberBandRect;

    // ── Connection endpoint dragging ──────────────────────────────────────────
    private bool               _isDraggingEndpoint;
    private ConnectionArtifact? _draggingConnection;
    private bool               _draggingIsSource;   // true = dragging source handle
    private PointF             _endpointDragPoint;
    private bool _isDrawingConnection;
    private PointF? _connectionStartPoint;
    private PointF? _connectionDragPoint;
    private FlowArtifact? _connectionSourceArtifact;
    private ConnectionPort? _connectionSourcePort;

    // ── Hover ─────────────────────────────────────────────────────────────────
    private Guid? _hoveredArtifactId;

    // ── Inline label editing ──────────────────────────────────────────────────
    private FlowArtifact? _editingLabelArtifact;
    private string _editingLabelValue = string.Empty;

    // ── Properties modal ──────────────────────────────────────────────────────
    private bool _showProperties;
    private FlowArtifact? _propertiesArtifact;

    // ── Validation ────────────────────────────────────────────────────────────
    private bool _showValidation;
    private ValidationResult? _validationResult;
    private bool _showRightPanel => _showValidation && _validationResult != null;

    // ── Confirm New ───────────────────────────────────────────────────────────
    private bool _showConfirmNew;

    // ── Title editing ─────────────────────────────────────────────────────────
    private bool   _isEditingTitle    = false;
    private string _editingTitleValue = string.Empty;

    // ── JS interop self-reference ─────────────────────────────────────────────
    private DotNetObjectReference<FlowChartCanvas>? _selfRef;

    private string _canvasCursor =>
        _activePlacementType.HasValue ? "crosshair" :
        _isDraggingArtifact           ? "grabbing"  :
        _isDrawingConnection          ? "crosshair" :
        "default";

    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);

    // ─────────────────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        ChartService.StateChanged += OnChartStateChanged;
        ChartService.History.StateChanged += OnChartStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("registerCanvasComponent", _selfRef);
        }
    }

    /// <summary>Called from JS when a connection line is double-clicked.</summary>
    [JSInvokable]
    public void OnConnectionDblClick(string connIdStr)
    {
        if (!Guid.TryParse(connIdStr, out var connId)) return;
        var conn = ChartService.Chart.FindConnection(connId);
        if (conn == null) return;
        ChartService.ClearSelection();
        conn.IsSelected     = true;
        _propertiesArtifact = conn;
        _showProperties     = true;
        InvokeAsync(StateHasChanged);
    }

    private void OnChartStateChanged() => InvokeAsync(StateHasChanged);

    // ── Convert SVG event coords to logical canvas coords ─────────────────────
    // MouseEventArgs.OffsetX/Y are relative to the SVG element — exactly what we need.
    // We only need to divide by zoom because the SVG has a scale() transform applied.
    private PointF ToCanvas(MouseEventArgs e) =>
        new(e.OffsetX / _zoom, e.OffsetY / _zoom);

    // ── Snap ──────────────────────────────────────────────────────────────────
    private double Snap(double v)
    {
        if (!_snapToGrid || ChartService.Chart.GridSize <= 0) return v;
        var g = ChartService.Chart.GridSize;
        return Math.Round(v / g) * g;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TOOLBAR
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleNew()
    {
        if (ChartService.HasUnsavedChanges) _showConfirmNew = true;
        else { ChartService.New(); CancelPlacement(); }
    }

    private void ConfirmNew()
    {
        _showConfirmNew = false;
        ChartService.New();
        CancelPlacement();
    }

    private void HandleLoad() =>
        JS.InvokeVoidAsync("triggerClick", "fc-file-input");

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        ChartService.LoadFromJson(json);
        CancelPlacement();
    }

    private async Task HandleSave()
    {
        _validationResult = Validator.Validate(ChartService.Chart);
        if (!_validationResult.IsValid)
        {
            _showValidation = true;
            return;
        }
        var json = ChartService.SaveToJson();
        await JS.InvokeVoidAsync("downloadFile",
            $"{ChartService.Chart.Title}.json", "application/json", json);
    }

    // ── File menu ─────────────────────────────────────────────────────────────
    private bool _fileMenuOpen   = false;
    private bool _exportMenuOpen = false;
    private bool _showSaveAs     = false;
    private string _saveAsFileName = string.Empty;

    /// <summary>Called from JS when user clicks outside the file menu.</summary>
    [JSInvokable]
    public void CloseFileMenu()
    {
        if (_fileMenuOpen || _exportMenuOpen)
        {
            _fileMenuOpen   = false;
            _exportMenuOpen = false;
            InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleFileMenu() => _fileMenuOpen = !_fileMenuOpen;

    private void HandleFileMenuLoad()
    {
        _fileMenuOpen = false;
        HandleLoad();
    }

    private async Task HandleFileMenuSave()
    {
        _fileMenuOpen = false;
        await HandleSave();
    }

    private void HandleFileMenuSaveAs()
    {
        _fileMenuOpen      = false;
        _saveAsFileName    = ChartService.Chart.Title;
        _showSaveAs        = true;
    }

    private async Task HandleExportSvg()
    {
        _fileMenuOpen    = false;
        _exportMenuOpen  = false;
        var title = ChartService.Chart.Title;
        await JS.InvokeVoidAsync("exportCanvasSvg", "fc-svg-canvas", $"{title}.svg");
    }

    private async Task HandleExportPng()
    {
        _fileMenuOpen    = false;
        _exportMenuOpen  = false;
        var title = ChartService.Chart.Title;
        await JS.InvokeVoidAsync("exportCanvasPng", "fc-svg-canvas", $"{title}.png", 2);
    }

    private async Task CommitSaveAs()
    {
        _showSaveAs = false;
        var fileName = string.IsNullOrWhiteSpace(_saveAsFileName)
            ? ChartService.Chart.Title
            : _saveAsFileName.Trim();

        // Validate first
        _validationResult = Validator.Validate(ChartService.Chart);
        if (!_validationResult.IsValid)
        {
            _showValidation = true;
            return;
        }

        var json = ChartService.SaveToJsonWithoutChangingTitle();
        await JS.InvokeVoidAsync("downloadFile",
            $"{fileName}.json", "application/json", json);
    }
    private void HandleArtifactMouseUp(MouseEventArgs e, FlowArtifact artifact)
    {
        if (!_isDraggingEndpoint || _draggingConnection == null) return;

        var pos = ToCanvas(e);

        // Find the nearest valid port on this artifact
        ConnectionPort? bestPort  = null;
        double          bestDist  = double.MaxValue;
        const double    snapRadius = 40.0;

        foreach (var port in artifact.Ports)
        {
            var dist = Math.Sqrt(
                Math.Pow(pos.X - port.Position.X, 2) +
                Math.Pow(pos.Y - port.Position.Y, 2));

            if (dist > snapRadius) continue;

            var isSource = _draggingIsSource;

            // Direction check
            if (artifact.Type != ArtifactType.OnPageConnector)
            {
                if (isSource  && port.Direction != PortDirection.Output) continue;
                if (!isSource && port.Direction != PortDirection.Input)  continue;
            }

            // No self-loop
            var otherId = isSource
                ? _draggingConnection.TargetArtifactId
                : _draggingConnection.SourceArtifactId;
            if (artifact.Id == otherId) continue;

            // Loop validation
            if (_draggingConnection.IsLoopLine)
            {
                if (isSource  && !artifact.CanBeLoopBegin) continue;
                if (!isSource && !artifact.CanBeLoopEnd)   continue;
            }

            if (dist < bestDist) { bestDist = dist; bestPort = port; }
        }

        if (bestPort != null)
        {
            var newSourceId   = _draggingIsSource ? artifact.Id                           : _draggingConnection.SourceArtifactId;
            var newSourcePort = _draggingIsSource ? bestPort.Id                           : _draggingConnection.SourcePortId;
            var newTargetId   = _draggingIsSource ? _draggingConnection.TargetArtifactId  : artifact.Id;
            var newTargetPort = _draggingIsSource ? _draggingConnection.TargetPortId      : bestPort.Id;

            ChartService.ReconnectConnection(
                _draggingConnection,
                newSourceId, newSourcePort,
                newTargetId, newTargetPort);
        }

        _isDraggingEndpoint = false;
        _draggingConnection = null;
    }

    /// <summary>Called from JS global mouseup — handles endpoint drop anywhere on canvas.</summary>
    [JSInvokable]
    public void OnGlobalMouseUp(double clientX, double clientY, double rectLeft, double rectTop)
    {
        if (!_isDraggingEndpoint || _draggingConnection == null) return;

        var canvasX = (clientX - rectLeft) / _zoom;
        var canvasY = (clientY - rectTop)  / _zoom;

        foreach (var artifact in ChartService.Chart.Artifacts)
        {
            if (artifact.Type == ArtifactType.Connection) continue;

            ConnectionPort? bestPort = null;
            double bestDist = double.MaxValue;
            const double snapRadius = 50.0;

            foreach (var port in artifact.Ports)
            {
                var dist = Math.Sqrt(
                    Math.Pow(canvasX - port.Position.X, 2) +
                    Math.Pow(canvasY - port.Position.Y, 2));

                if (dist > snapRadius) continue;

                var isSource = _draggingIsSource;

                if (artifact.Type != ArtifactType.OnPageConnector)
                {
                    if (isSource  && port.Direction != PortDirection.Output) continue;
                    if (!isSource && port.Direction != PortDirection.Input)  continue;
                }

                var otherId = isSource
                    ? _draggingConnection.TargetArtifactId
                    : _draggingConnection.SourceArtifactId;
                if (artifact.Id == otherId) continue;

                if (_draggingConnection.IsLoopLine)
                {
                    if (isSource  && !artifact.CanBeLoopBegin) continue;
                    if (!isSource && !artifact.CanBeLoopEnd)   continue;
                }

                if (dist < bestDist) { bestDist = dist; bestPort = port; }
            }

            if (bestPort != null)
            {
                var newSourceId   = _draggingIsSource ? artifact.Id                          : _draggingConnection.SourceArtifactId;
                var newSourcePort = _draggingIsSource ? bestPort.Id                          : _draggingConnection.SourcePortId;
                var newTargetId   = _draggingIsSource ? _draggingConnection.TargetArtifactId : artifact.Id;
                var newTargetPort = _draggingIsSource ? _draggingConnection.TargetPortId     : bestPort.Id;

                ChartService.ReconnectConnection(
                    _draggingConnection,
                    newSourceId, newSourcePort,
                    newTargetId, newTargetPort);
                break;
            }
        }

        _isDraggingEndpoint = false;
        _draggingConnection = null;
        InvokeAsync(StateHasChanged);
    }

    private void HandleEndpointDragStart(
        (MouseEventArgs E, ConnectionArtifact Conn, bool IsSource) args)
    {
        _isDraggingEndpoint  = true;
        _draggingConnection  = args.Conn;
        _draggingIsSource    = args.IsSource;
        _endpointDragPoint   = ToCanvas(args.E);
        _isDraggingArtifact  = false;
        _isDrawingConnection = false;
    }
    private void HandleDelete()        => ChartService.DeleteSelected();
    private void HandleSelectAll()     => ChartService.SelectAll();
    private void HandleUndo()        => ChartService.Undo();
    private void HandleRedo()        => ChartService.Redo();
    private void HandleSnapToggle()  => _snapToGrid = !_snapToGrid;
    private void HandleAlignVertical()   => ChartService.AlignVerticalCenters();
    private void HandleAlignHorizontal() => ChartService.AlignHorizontalCenters();

    private void HandleZoomIn()  => _zoom = Math.Min(ZoomMax, Math.Round(_zoom + ZoomStep, 2));
    private void HandleZoomOut() => _zoom = Math.Max(ZoomMin, Math.Round(_zoom - ZoomStep, 2));
    private void HandleZoomFit() => _zoom = 1.0;

    private void HandleValidate()
    {
        _validationResult = Validator.Validate(ChartService.Chart);
        _showValidation = true;
    }

    private void HandleWheel(WheelEventArgs e)
    {
        if (e.DeltaY < 0) HandleZoomIn(); else HandleZoomOut();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PLACEMENT MODE
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleStencilSelect(ArtifactType type)
    {
        _activePlacementType = (_activePlacementType == type && _activeUfaName == null) ? null : type;
        _activeUfaName       = null;
        _ghostPoint          = null;
    }

    private void HandleUfaStencilSelect(string ufaName)
    {
        _activePlacementType = (_activeUfaName == ufaName) ? null : ArtifactType.UserDefined;
        _activeUfaName       = (_activeUfaName == ufaName) ? null : ufaName;
        _ghostPoint          = null;
    }

    private void CancelPlacement()
    {
        _activePlacementType = null;
        _activeUfaName       = null;
        _ghostPoint          = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CANVAS MOUSE EVENTS  (events come from the SVG element)
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleCanvasMouseDown(MouseEventArgs e)
    {
        if (e.Button != 0) return;

        var pos = ToCanvas(e);

        // ── Placement mode: drop artifact ────────────────────────────────────
        if (_activePlacementType.HasValue)
        {
            if (_activePlacementType == ArtifactType.UserDefined && _activeUfaName != null)
            {
                // Place a UFA
                var ufa = UfaLoader.CreateInstance(_activeUfaName, 0, 0);
                if (ufa != null)
                {
                    var ax = Snap(pos.X - ufa.Width  / 2);
                    var ay = Snap(pos.Y - ufa.Height / 2);
                    ufa.X  = ax; ufa.Y = ay;
                    ChartService.AddUfaArtifact(ufa);
                }
            }
            else
            {
                var def = ArtifactFactory.Create(_activePlacementType.Value, 0, 0);
                var ax  = Snap(pos.X - def.Width  / 2);
                var ay  = Snap(pos.Y - def.Height / 2);
                ChartService.AddArtifact(_activePlacementType.Value, ax, ay);
            }
            if (!e.CtrlKey) CancelPlacement();
            return;
        }

        // ── Normal mode: start rubber-band ───────────────────────────────────
        ChartService.ClearSelection();
        _rubberBandStart  = pos;
        _isRubberBanding  = true;
        _rubberBandRect   = null;
    }

    private void HandleMouseMove(MouseEventArgs e)
    {
        var pos = ToCanvas(e);

        // Ghost preview while in placement mode
        if (_activePlacementType.HasValue)
        {
            _ghostPoint = new PointF(Snap(pos.X), Snap(pos.Y));
            return;
        }

        // Drag connection endpoint
        if (_isDraggingEndpoint)
        {
            _endpointDragPoint = pos;
            return;
        }

        // Drag selected artifacts
        if (_isDraggingArtifact)
        {
            _dragMoved = true;
            var dx = pos.X - _dragStartCanvas.X;
            var dy = pos.Y - _dragStartCanvas.Y;
            foreach (var a in ChartService.Chart.Artifacts.Where(a => a.IsSelected))
            {
                if (_dragStartPositions.TryGetValue(a.Id, out var start))
                {
                    a.X = Snap(start.X + dx);
                    a.Y = Snap(start.Y + dy);
                }
            }
            return;
        }

        // Update in-progress connection line
        if (_isDrawingConnection)
        {
            _connectionDragPoint = pos;
            return;
        }

        // Update rubber-band rect
        if (_isRubberBanding)
        {
            var x = Math.Min(_rubberBandStart.X, pos.X);
            var y = Math.Min(_rubberBandStart.Y, pos.Y);
            var w = Math.Abs(pos.X - _rubberBandStart.X);
            var h = Math.Abs(pos.Y - _rubberBandStart.Y);
            _rubberBandRect = new RectF(x, y, w, h);
        }
    }

    private void HandleMouseUp(MouseEventArgs e)
    {
        var pos = ToCanvas(e);

        // Commit endpoint drag — find port under mouse
        if (_isDraggingEndpoint && _draggingConnection != null)
        {
            var dropped = false;
            foreach (var artifact in ChartService.Chart.Artifacts)
            {
                if (artifact.Type == ArtifactType.Connection) continue;
                foreach (var port in artifact.Ports)
                {
                    var dist = Math.Sqrt(
                        Math.Pow(pos.X - port.Position.X, 2) +
                        Math.Pow(pos.Y - port.Position.Y, 2));

                    if (dist > 25) continue; // snap radius

                    // Validate direction
                    var isSource = _draggingIsSource;
                    if (artifact.Type != ArtifactType.OnPageConnector)
                    {
                        if (isSource  && port.Direction != PortDirection.Output) continue;
                        if (!isSource && port.Direction != PortDirection.Input)  continue;
                    }

                    // No self-loop
                    var otherId = isSource
                        ? _draggingConnection.TargetArtifactId
                        : _draggingConnection.SourceArtifactId;
                    if (artifact.Id == otherId) break;

                    // Loop line validation
                    if (_draggingConnection.IsLoopLine)
                    {
                        if (isSource  && !artifact.CanBeLoopBegin) break;
                        if (!isSource && !artifact.CanBeLoopEnd)   break;
                    }

                    // Commit reconnection
                    var newSourceId   = isSource ? artifact.Id                            : _draggingConnection.SourceArtifactId;
                    var newSourcePort = isSource ? port.Id                                : _draggingConnection.SourcePortId;
                    var newTargetId   = isSource ? _draggingConnection.TargetArtifactId  : artifact.Id;
                    var newTargetPort = isSource ? _draggingConnection.TargetPortId       : port.Id;

                    ChartService.ReconnectConnection(
                        _draggingConnection,
                        newSourceId, newSourcePort,
                        newTargetId, newTargetPort);

                    dropped = true;
                    break;
                }
                if (dropped) break;
            }
            // If not dropped on a valid port — snap back (do nothing, original values unchanged)
            _isDraggingEndpoint = false;
            _draggingConnection = null;
            return;
        }

        // Commit artifact move as undoable command
        if (_isDraggingArtifact && _dragMoved)
        {
            var dx = pos.X - _dragStartCanvas.X;
            var dy = pos.Y - _dragStartCanvas.Y;

            var moves = ChartService.Chart.Artifacts
                .Where(a => a.IsSelected && _dragStartPositions.ContainsKey(a.Id))
                .Select(a =>
                {
                    var start = _dragStartPositions[a.Id];
                    var nx = Snap(start.X + dx);
                    var ny = Snap(start.Y + dy);
                    a.X = start.X; a.Y = start.Y; // reset so command applies from known state
                    return (a, start.X, start.Y, nx, ny);
                }).ToList();

            if (moves.Any()) ChartService.MoveArtifacts(moves);
        }

        _isDraggingArtifact   = false;
        _dragMoved            = false;
        _dragStartPositions.Clear();

        // Commit rubber-band selection
        if (_isRubberBanding)
        {
            if (_rubberBandRect.HasValue &&
               (_rubberBandRect.Value.Width > 5 || _rubberBandRect.Value.Height > 5))
                ChartService.SelectInRect(_rubberBandRect.Value);

            _isRubberBanding = false;
            _rubberBandRect  = null;
        }

        // Cancel any in-progress connection draw
        _isDrawingConnection      = false;
        _connectionDragPoint      = null;
        _connectionStartPoint     = null;
        _connectionSourceArtifact = null;
        _connectionSourcePort     = null;
    }

    private void HandleMouseLeave(MouseEventArgs e)
    {
        _ghostPoint = null;
        if (_isDraggingArtifact)
        {
            _isDraggingArtifact = false;
            _dragMoved = false;
            _dragStartPositions.Clear();
        }
        if (_isDraggingEndpoint)
        {
            _isDraggingEndpoint = false;
            _draggingConnection = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ARTIFACT EVENTS
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleArtifactMouseDown(MouseEventArgs e, FlowArtifact artifact)
    {
        if (e.Button != 0) return;

        // In placement mode, let the canvas click handler deal with it
        if (_activePlacementType.HasValue) return;

        // Select
        if (e.CtrlKey)
            artifact.IsSelected = !artifact.IsSelected;
        else if (!artifact.IsSelected)
            ChartService.SelectArtifact(artifact.Id);

        // Begin drag
        _isDraggingArtifact   = true;
        _dragMoved            = false;
        _dragStartCanvas      = ToCanvas(e);
        _dragStartPositions   = ChartService.Chart.Artifacts
            .Where(a => a.IsSelected)
            .ToDictionary(a => a.Id, a => (a.X, a.Y));

        _isRubberBanding = false;
    }

    private void HandleArtifactDblClick(MouseEventArgs e, FlowArtifact artifact)
    {
        // Double click always opens Properties modal for all artifact types
        if (_editingLabelArtifact != null)
        {
            _editingLabelArtifact.IsEditingLabel = false;
            _editingLabelArtifact = null;
        }
        _propertiesArtifact = artifact;
        _showProperties     = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONNECTION PORT EVENTS
    // ─────────────────────────────────────────────────────────────────────────

    private void HandlePortMouseDown(MouseEventArgs e, FlowArtifact artifact, ConnectionPort port)
    {
        // OnPageConnector: any port can be a source (bidirectional)
        // All other artifacts: only Output ports can be a source
        var isBiDir = artifact.Type == ArtifactType.OnPageConnector;
        if (!isBiDir && port.Direction != PortDirection.Output) return;

        _isDrawingConnection      = true;
        _isDraggingArtifact       = false;
        _connectionSourceArtifact = artifact;
        _connectionSourcePort     = port;
        _connectionStartPoint     = port.Position;
        _connectionDragPoint      = port.Position;
    }

    private void HandlePortMouseUp(MouseEventArgs e, FlowArtifact artifact, ConnectionPort port)
    {
        if (!_isDrawingConnection) return;
        if (_connectionSourceArtifact == null)            { ResetConnectionDraw(); return; }
        if (artifact.Id == _connectionSourceArtifact.Id) { ResetConnectionDraw(); return; }

        // OnPageConnector: any port can be a target (bidirectional)
        // All other artifacts: only Input ports can be a target
        var isBiDir = artifact.Type == ArtifactType.OnPageConnector;
        if (!isBiDir && port.Direction != PortDirection.Input) { ResetConnectionDraw(); return; }

        ChartService.AddConnection(
            _connectionSourceArtifact.Id, _connectionSourcePort!.Id,
            artifact.Id, port.Id);

        ResetConnectionDraw();
    }

    private void ResetConnectionDraw()
    {
        _isDrawingConnection      = false;
        _connectionDragPoint      = null;
        _connectionStartPoint     = null;
        _connectionSourceArtifact = null;
        _connectionSourcePort     = null;
    }

    private void HandleConnectionClick(ConnectionArtifact conn)
    {
        // Single click — just select the line (allows Delete key to work)
        ChartService.ClearSelection();
        conn.IsSelected = true;
    }

    private void HandleConnectionDblClick(ConnectionArtifact conn)
    {
        // Double click — open properties
        ChartService.ClearSelection();
        conn.IsSelected     = true;
        _propertiesArtifact = conn;
        _showProperties     = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TITLE EDITING
    // ─────────────────────────────────────────────────────────────────────────

    private void StartTitleEdit()
    {
        _editingTitleValue = ChartService.Chart.Title;
        _isEditingTitle    = true;
        JS.InvokeVoidAsync("focusElement", "fc-title-input");
    }

    private void CommitTitleEdit()
    {
        _isEditingTitle = false;
        var newTitle    = _editingTitleValue.Trim();
        if (string.IsNullOrWhiteSpace(newTitle)) return;
        if (newTitle == ChartService.Chart.Title)  return;

        ChartService.Chart.Title      = newTitle;
        ChartService.HasUnsavedChanges = true;
        ChartService.NotifyStateChanged();
    }

    private void HandleTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")  CommitTitleEdit();
        if (e.Key == "Escape")
        {
            _isEditingTitle = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INLINE LABEL EDITING
    // ─────────────────────────────────────────────────────────────────────────

    private void StartLabelEdit(FlowArtifact artifact)
    {
        _editingLabelArtifact = artifact;
        _editingLabelValue    = artifact.Label;
        artifact.IsEditingLabel = true;
        JS.InvokeVoidAsync("focusElement", "fc-label-input");
    }

    private void CommitLabelEdit()
    {
        if (_editingLabelArtifact == null) return;
        _editingLabelArtifact.IsEditingLabel = false;
        ChartService.EditLabel(_editingLabelArtifact, _editingLabelValue);
        _editingLabelArtifact = null;
    }

    private void HandleLabelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            CommitLabelEdit();
        else if (e.Key == "Escape")
        {
            if (_editingLabelArtifact != null)
                _editingLabelArtifact.IsEditingLabel = false;
            _editingLabelArtifact = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROPERTIES MODAL SAVE
    // ─────────────────────────────────────────────────────────────────────────

    private void HandlePropertiesSave(
        (FlowArtifact Artifact, string Label, List<PropertyValue> Values,
         bool CanBeLoopBegin, bool CanBeLoopEnd) args)
    {
        // Update loop participation flags
        args.Artifact.CanBeLoopBegin = args.CanBeLoopBegin;
        args.Artifact.CanBeLoopEnd   = args.CanBeLoopEnd;
        ChartService.EditProperties(args.Artifact, args.Label, args.Values);
        _showProperties     = false;
        _propertiesArtifact = null;
    }

    private void HandleConnectionPropertiesSave(
        (ConnectionArtifact Conn, LineType LineType,
         ArrowTerminator Start, ArrowTerminator End, string Label,
         LoopSide LoopSide, string LoopLabel, string LineColor) args)
    {
        ChartService.EditConnectionProperties(
            args.Conn, args.LineType, args.Start, args.End,
            args.Label, args.LoopSide, args.LoopLabel, args.LineColor);
        _showProperties     = false;
        _propertiesArtifact = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KEYBOARD
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":                      CancelPlacement();   break;
            case "Delete" or "Backspace":       HandleDelete();      break;
            case "z" when e.CtrlKey:            HandleUndo();        break;
            case "y" when e.CtrlKey:            HandleRedo();        break;
            case "a" when e.CtrlKey:            HandleSelectAll();   break;
            case "s" when e.CtrlKey:          _ = HandleSave();      break;
            case "+" or "=":                    HandleZoomIn();      break;
            case "-":                           HandleZoomOut();     break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONNECTION POINT RESOLUTION
    // ─────────────────────────────────────────────────────────────────────────

    private PointF? GetConnectionStartPoint(ConnectionArtifact conn)
    {
        if (conn.SourceArtifactId == null) return null;
        var src = ChartService.Chart.FindArtifact(conn.SourceArtifactId.Value);
        if (src == null) return null;
        return src.Ports.FirstOrDefault(p => p.Id == conn.SourcePortId)?.Position ?? src.Center;
    }

    private PointF? GetConnectionEndPoint(ConnectionArtifact conn)
    {
        if (conn.TargetArtifactId == null) return null;
        var tgt = ChartService.Chart.FindArtifact(conn.TargetArtifactId.Value);
        if (tgt == null) return null;
        return tgt.Ports.FirstOrDefault(p => p.Id == conn.TargetPortId)?.Position ?? tgt.Center;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VALIDATION FOCUS
    // ─────────────────────────────────────────────────────────────────────────

    private void FocusArtifact(Guid? id)
    {
        if (id == null) return;
        ChartService.ClearSelection();
        ChartService.SelectArtifact(id.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DISPOSE
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        ChartService.StateChanged -= OnChartStateChanged;
        ChartService.History.StateChanged -= OnChartStateChanged;
        JS.InvokeVoidAsync("unregisterCanvasComponent");
        _selfRef?.Dispose();
    }
}
