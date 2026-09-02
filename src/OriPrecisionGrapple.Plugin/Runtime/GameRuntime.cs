using BepInEx.Logging;
using OriPrecisionGrapple.Core;
using OriPrecisionGrapple.Core.Diagnostics;
using System.Reflection;

namespace OriPrecisionGrapple.Runtime;

internal sealed class GameRuntime : IDisposable
{
    private const int MaxTrackedGrappleCandidates = 24;

    private readonly IRuntimeSettings _settings;
    private readonly ManualLogSource _log;
    private readonly Func<bool> _isRightButtonHeld;
    private readonly InputRoutingState _routingState = new();

    private GameTypeCatalog? _types;
    private object? _latestSpiritLeash;
    private bool _insideTargetSearch;
    private double _aimX;
    private double _aimY;
    private bool _hasAimDirection;
    private bool _insideInputRouting;
    private InputRoute _lastLoggedRoute;
    private object? _originalHorizontal;
    private object? _originalVertical;
    private bool _gameInputOverridden;
    private bool _loggedMouseInputOverride;
    private readonly List<GrappleCandidateObservation> _grappleCandidates = new();
    private object? _latestBashAttack;
    private object? _latestBashTarget;
    private DebugOverlayRenderer? _debugOverlay;
    private long _targetSearchSequence;
    private long _targetSearchCompletedAt;
    private bool _snapshotFailureLogged;
    private bool _overlayCallbackLogged;
    private DiagnosticPipeServer? _diagnosticPipe;
    private bool _diagnosticUpdateLogged;

    public GameRuntime(
        IRuntimeSettings settings,
        ManualLogSource log,
        Func<bool>? isRightButtonHeld = null)
    {
        _settings = settings;
        _log = log;
        _isRightButtonHeld = isRightButtonHeld ?? PhysicalMouse.IsRightButtonHeldByGameWindow;
    }

    public void Attach(GameTypeCatalog types)
    {
        _types = types;
        if (_settings.ExternalMonitorEnabled)
        {
            _diagnosticPipe = new DiagnosticPipeServer(_log);
            _log.LogInfo($"External diagnostics pipe '{DiagnosticProtocol.PipeName}' is ready for the monitor.");
        }

        if (_settings.ShowOverlay)
        {
            _log.LogInfo("Experimental in-game diagnostics renderer will initialize on the first GameController.OnGUI callback.");
        }
    }

    public void Dispose()
    {
        _diagnosticPipe?.Dispose();
        _diagnosticPipe = null;
    }

    public void BeginTargetSearch(object spiritLeash)
    {
        RestoreGameInput();
        _grappleCandidates.Clear();
        _targetSearchSequence++;
        _latestSpiritLeash = spiritLeash;
        _insideTargetSearch = true;
        _hasAimDirection = TryGetMouseAimDirection(out _aimX, out _aimY);
        ApplyMouseAimToGameInput();
    }

    public void EndTargetSearch(object spiritLeash)
    {
        _latestSpiritLeash = spiritLeash;
        RestoreGameInput();
        _insideTargetSearch = false;
        _hasAimDirection = false;
        _targetSearchCompletedAt = Environment.TickCount64;
    }

    public void CaptureGrappleCandidate(
        object attackable,
        float distance,
        float angleDifference,
        bool hasInputDirection,
        float cost)
    {
        if (!_insideTargetSearch || _grappleCandidates.Count >= MaxTrackedGrappleCandidates)
        {
            return;
        }

        var existing = _grappleCandidates.FirstOrDefault(candidate =>
            ReflectionAccess.SameNativeObject(candidate.Attackable, attackable));
        if (existing is not null)
        {
            existing.Distance = distance;
            existing.AngleDifference = angleDifference;
            existing.HasInputDirection = hasInputDirection;
            existing.Cost = cost;
            return;
        }

        _grappleCandidates.Add(new GrappleCandidateObservation(
            attackable,
            distance,
            angleDifference,
            hasInputDirection,
            cost));
    }

    public void CaptureBashTarget(object bashAttack, object? target)
    {
        _latestBashAttack = bashAttack;
        _latestBashTarget = target;
    }

    public void DrawDiagnostics()
    {
        if (!_settings.ShowOverlay)
        {
            return;
        }

        if (!_overlayCallbackLogged)
        {
            _overlayCallbackLogged = true;
            _log.LogInfo("GameController.OnGUI diagnostics callback reached.");
        }

        if (_snapshotFailureLogged || _types is null)
        {
            return;
        }

        try
        {
            var snapshot = BuildDebugSnapshot();
            if (_debugOverlay is null)
            {
                _debugOverlay = DebugOverlayRenderer.TryCreate(_types, _settings, _log, out var status);
                if (_debugOverlay is null)
                {
                    _snapshotFailureLogged = true;
                    _log.LogError($"Diagnostics screen renderer unavailable: {status}");
                    return;
                }

                _log.LogInfo($"Diagnostics screen renderer ready: {status}");
            }

            _debugOverlay.Draw(snapshot);
        }
        catch (Exception exception)
        {
            _snapshotFailureLogged = true;
            _log.LogError($"Diagnostics snapshot disabled after a read failure: {exception}");
        }
    }

    public void UpdateDiagnostics()
    {
        if (!_settings.ExternalMonitorEnabled || _diagnosticPipe is null || _snapshotFailureLogged)
        {
            return;
        }

        if (!_diagnosticUpdateLogged)
        {
            _diagnosticUpdateLogged = true;
            _log.LogInfo("External diagnostics are publishing on the GameController.Update tick.");
        }

        try
        {
            _diagnosticPipe.Publish(ToDiagnosticFrame(BuildDebugSnapshot()));
        }
        catch (Exception exception)
        {
            _snapshotFailureLogged = true;
            _log.LogError($"External diagnostics publishing disabled after a snapshot failure: {exception}");
        }
    }

    private static DiagnosticFrame ToDiagnosticFrame(DebugSnapshot snapshot) => new()
    {
        ScreenWidth = snapshot.ScreenWidth,
        ScreenHeight = snapshot.ScreenHeight,
        CursorX = snapshot.Cursor.X,
        CursorY = snapshot.Cursor.Y,
        EffectiveRadius = snapshot.EffectiveRadius,
        TargetMarkerRadius = snapshot.TargetMarkerRadius,
        PrecisionHit = snapshot.PrecisionHit,
        GrappleState = snapshot.GrappleState,
        GrappleRangeCenterX = snapshot.GrappleRangeCenter?.X,
        GrappleRangeCenterY = snapshot.GrappleRangeCenter?.Y,
        NormalRangeRadiusX = snapshot.NormalRangeRadiusX,
        NormalRangeRadiusY = snapshot.NormalRangeRadiusY,
        RetainedRangeRadiusX = snapshot.RetainedRangeRadiusX,
        RetainedRangeRadiusY = snapshot.RetainedRangeRadiusY,
        GrappleTargetX = snapshot.GrappleTarget?.X,
        GrappleTargetY = snapshot.GrappleTarget?.Y,
        Lines = snapshot.Lines.ToArray(),
        Markers = snapshot.Markers.Select(marker => new DiagnosticMarker
        {
            Label = marker.Label,
            Kind = marker.Kind.ToString(),
            State = marker.State,
            Detail = marker.Detail,
            X = marker.Point.X,
            Y = marker.Point.Y,
        }).ToArray(),
    };

    public bool TryGetMouseFacing(out bool faceLeft)
    {
        faceLeft = false;
        if (!_settings.Enabled || !_insideTargetSearch || !_hasAimDirection)
        {
            return false;
        }

        faceLeft = _aimX < 0.0;
        return true;
    }

    public bool RouteButton(object buttonInput, bool originalResult)
    {
        if (!_settings.Enabled || _types is null || _insideInputRouting)
        {
            return originalResult;
        }

        try
        {
            _insideInputRouting = true;
            var playerInput = ReflectionAccess.GetStatic(_types.PlayerInput, "Instance");
            if (playerInput is null || !ReflectionAccess.GetBoolean(playerInput, false, "Active"))
            {
                _routingState.Reset();
                return originalResult;
            }

            var bashInput = ReflectionAccess.Get(playerInput, "Bash");
            var grappleInput = ReflectionAccess.Get(playerInput, "Grapple");
            var isBash = ReflectionAccess.SameNativeObject(buttonInput, bashInput);
            var isGrapple = ReflectionAccess.SameNativeObject(buttonInput, grappleInput);
            if (!isBash && !isGrapple)
            {
                return originalResult;
            }

            var rightButtonHeld = _isRightButtonHeld();
            if (!rightButtonHeld)
            {
                _routingState.Reset();
                _lastLoggedRoute = InputRoute.None;
                return originalResult;
            }

            var preciseTarget = _routingState.IsPressActive
                ? _routingState.CurrentRoute == InputRoute.Grapple
                : IsPreciseGrappleTarget();
            var route = _routingState.Update(
                rightButtonHeld: true,
                hasPreciseGrappleTarget: preciseTarget,
                grappleAvailable: preciseTarget,
                bashAvailable: true);

            LogRouteChange(route, preciseTarget);
            return isBash ? route == InputRoute.Bash : route == InputRoute.Grapple;
        }
        catch (Exception exception)
        {
            _log.LogError($"Input routing failed; preserving the original result: {exception}");
            return originalResult;
        }
        finally
        {
            _insideInputRouting = false;
        }
    }

    public bool FilterGrappleMark(bool originalResult, object spiritLeash)
    {
        if (!originalResult || !_settings.Enabled || !_settings.HideImpreciseMark)
        {
            return originalResult;
        }

        _latestSpiritLeash = spiritLeash;
        return IsPreciseGrappleTarget();
    }

    public void ResetInput()
    {
        _routingState.Reset();
        _lastLoggedRoute = InputRoute.None;
    }

    private DebugSnapshot BuildDebugSnapshot()
    {
        if (_types is null)
        {
            return new DebugSnapshot();
        }

        var width = Convert.ToInt32(TryInvokeStatic(_types.Screen, "get_width") ?? 0);
        var height = Convert.ToInt32(TryInvokeStatic(_types.Screen, "get_height") ?? 0);
        var viewport = new Viewport(width, height);
        var mouseObject = TryInvokeStatic(_types.MoonInput, "get_mousePosition");
        var cursor = mouseObject is null ? new ScreenPoint(double.NaN, double.NaN, 1.0) : ReadPoint(mouseObject);
        var effectiveRadius = height > 0
            ? _settings.RadiusPixels * height / _settings.ReferenceHeight
            : 0.0;
        var targetMarkerRadius = height > 0
            ? _settings.TargetMarkerRadiusPixels * height / _settings.ReferenceHeight
            : 0.0;

        var hasTarget = _latestSpiritLeash is not null &&
            ReflectionAccess.GetBoolean(_latestSpiritLeash, false, "HasTarget");
        var canLeash = _latestSpiritLeash is not null &&
            ReflectionAccess.GetBoolean(_latestSpiritLeash, false, "CanLeash");
        var hasTargetMetrics = TryGetPrecisionMetrics(out _, out var target, out _, out var targetScreenDistance);
        var precisionHit = hasTarget && canLeash && hasTargetMetrics && PrecisionHitTest.IsHit(
            cursor,
            target,
            viewport,
            _settings.RadiusPixels,
            _settings.ReferenceHeight);

        var playerInput = ReflectionAccess.GetStatic(_types.PlayerInput, "Instance");
        var playerInputActive = playerInput is not null && ReflectionAccess.GetBoolean(playerInput, false, "Active");
        var moveCooldownTimer = ReadNumber(_latestSpiritLeash, "MoveCooldownTimer", "_MoveCooldownTimer_k__BackingField");
        var providerCooldownTimer = ReadNumber(_latestSpiritLeash, "ProviderCooldownTimer", "_ProviderCooldownTimer_k__BackingField");
        var normalRange = ReadNumber(_latestSpiritLeash, "SpiritLeashRange");
        var currentRange = ReadNumber(_latestSpiritLeash, "SpiritLeashRangeCurrentTarget");
        var targetDistance = ReadNumber(_latestSpiritLeash, "DistanceFromOri");
        var hookAngle = ReadNumber(_latestSpiritLeash, "HookDirectionErrorAngle");
        var noInputHookAngle = ReadNumber(_latestSpiritLeash, "HookDirectionErrorAngleNoInput");
        var retainAngleBonus = ReadNumber(_latestSpiritLeash, "HookDirectionErrorAngleRetainTargetBonus");
        var facingAngle = ReadNumber(_latestSpiritLeash, "FacingDirectionErrorAngle");
        var retentionDuration = ReadNumber(_latestSpiritLeash, "DurationToKeepTargetWhileFacingAway");
        var sustainedCost = ReadNumber(_latestSpiritLeash, "SustainedTargetAdditionalCost");
        var state = _latestSpiritLeash is null
            ? "n/a"
            : ReflectionAccess.Get(_latestSpiritLeash, "m_currentState")?.ToString() ?? "n/a";
        var grappleState = GetGrappleState(
            hasTarget,
            canLeash,
            precisionHit,
            moveCooldownTimer,
            providerCooldownTimer,
            state);
        var globalBlockDetail = GetGlobalBlockDetail(grappleState, moveCooldownTimer, providerCooldownTimer, state);

        var targetLeash = _latestSpiritLeash is null
            ? null
            : ReflectionAccess.Get(_latestSpiritLeash, "m_targetLeash", "TargetLeash");
        var selectedAttackable = targetLeash is null
            ? null
            : ReflectionAccess.Get(targetLeash, "SpiritLeashAttackable");
        var retainedAttackable = _latestSpiritLeash is null
            ? null
            : ReflectionAccess.Get(_latestSpiritLeash, "lastTargetSpiritLeashAttackable");
        var selectedCandidate = _grappleCandidates.FirstOrDefault(candidate =>
            ReflectionAccess.SameNativeObject(candidate.Attackable, selectedAttackable));
        var selectedCost = selectedCandidate?.Cost ?? double.NaN;

        ScreenPoint? grappleRangeCenter = null;
        var normalRangeRadiusX = 0.0;
        var normalRangeRadiusY = 0.0;
        var retainedRangeRadiusX = 0.0;
        var retainedRangeRadiusY = 0.0;
        if (TryGetOriWorldPosition(out var rangeOrigin))
        {
            if (TryGetWorldRangeProjection(
                rangeOrigin!,
                normalRange,
                out var center,
                out normalRangeRadiusX,
                out normalRangeRadiusY))
            {
                grappleRangeCenter = center;
            }

            TryGetWorldRangeProjection(
                rangeOrigin!,
                currentRange,
                out _,
                out retainedRangeRadiusX,
                out retainedRangeRadiusY);
        }

        var markers = new List<DebugMarker>();
        var candidateIndex = 0;
        var unselectedIndex = 0;
        var selectedMarkerAdded = false;
        foreach (var candidate in _grappleCandidates)
        {
            var selected = ReflectionAccess.SameNativeObject(candidate.Attackable, selectedAttackable);
            var retained = ReflectionAccess.SameNativeObject(candidate.Attackable, retainedAttackable);
            if (!TryGetGrappleCandidateWorldPosition(candidate.Attackable, out var worldPosition) ||
                !TryWorldToScreen(worldPosition!, out var screenPosition))
            {
                continue;
            }

            if (selected && hasTargetMetrics)
            {
                screenPosition = target;
            }

            candidateIndex++;
            if (!selected)
            {
                unselectedIndex++;
            }
            var cursorHit = PrecisionHitTest.IsHit(
                cursor,
                screenPosition,
                viewport,
                _settings.RadiusPixels,
                _settings.ReferenceHeight);
            var candidateState = GetCandidateState(
                candidate,
                selected,
                retained,
                cursorHit,
                grappleState,
                normalRange,
                currentRange,
                hookAngle,
                noInputHookAngle,
                retainAngleBonus);
            var detail = GetCandidateDetail(
                candidate,
                candidateState,
                retained,
                selectedCost,
                effectiveRadius,
                cursor,
                screenPosition,
                normalRange,
                currentRange,
                hookAngle,
                noInputHookAngle,
                retainAngleBonus,
                globalBlockDetail);
            markers.Add(new DebugMarker(
                screenPosition,
                selected ? "G*" : $"G{unselectedIndex}",
                selected ? DebugMarkerKind.GrappleTarget : DebugMarkerKind.GrappleCandidate,
                candidateState,
                detail));
            selectedMarkerAdded |= selected;
        }

        if (hasTarget && hasTargetMetrics && !selectedMarkerAdded)
        {
            markers.Add(new DebugMarker(
                target,
                "G*",
                DebugMarkerKind.GrappleTarget,
                grappleState,
                string.IsNullOrEmpty(globalBlockDetail)
                    ? precisionHit ? "READY" : $"CURSOR {Format(targetScreenDistance)} > {Format(effectiveRadius)}"
                    : globalBlockDetail));
        }

        var bashRange = ReadNumber(_latestBashAttack, "Range");
        var bashDistance = double.NaN;
        var hasBashTarget = TryGetBashWorldPosition(out var bashWorld);
        if (hasBashTarget)
        {
            if (TryGetOriWorldPosition(out var oriWorld))
            {
                bashDistance = WorldDistance(oriWorld!, bashWorld!);
            }

            if (TryWorldToScreen(bashWorld!, out var bashScreen))
            {
                markers.Add(new DebugMarker(
                    bashScreen,
                    "B",
                    DebugMarkerKind.BashTarget,
                    DiagnosticMarkerStates.Bash,
                    $"BASH {Format(bashDistance)} / {Format(bashRange)}"));
            }
        }

        var searchAge = _targetSearchCompletedAt <= 0
            ? -1
            : Math.Max(0, Environment.TickCount64 - _targetSearchCompletedAt);
        var bashInRange = hasBashTarget && double.IsFinite(bashRange) && double.IsFinite(bashDistance) && bashDistance <= bashRange;

        var lines = new List<string>
        {
            $"Screen {width}x{height} | search #{_targetSearchSequence} | age {searchAge} ms",
            $"Mouse ({Format(cursor.X)}, {Format(cursor.Y)}) | aim ({Format(_aimX, 3)}, {Format(_aimY, 3)})",
            $"Input active {Flag(playerInputActive)} | RMB {Flag(_isRightButtonHeld())} | locked route {_routingState.CurrentRoute}",
            $"CanLeash {Flag(canLeash)} | vanilla HasTarget {Flag(hasTarget)} | state {state}",
            $"Cooldown move {Format(moveCooldownTimer)} | provider {Format(providerCooldownTimer)}",
            $"Range normal {Format(normalRange)} | retained {Format(currentRange)} | target distance {Format(targetDistance)}",
            $"Direction input {Format(hookAngle)} | no-input {Format(noInputHookAngle)} | retain +{Format(retainAngleBonus)} | facing {Format(facingAngle)}",
            $"Retention {Format(retentionDuration)} s | sustained cost {Format(sustainedCost)} | status {grappleState}",
            $"Grapple candidates {_grappleCandidates.Count} | screen-visible {candidateIndex}",
            $"Target/cursor delta {Format(targetScreenDistance)} px | threshold {Format(effectiveRadius)} px | precision {Flag(precisionHit)}",
            $"Bash target {Flag(hasBashTarget)} | distance {Format(bashDistance)} | range {Format(bashRange)} | in range {Flag(bashInRange)}",
            "Colors: green ready | red cursor miss | yellow selector | purple retained | gray range | magenta blocked",
            "Circles: large=mouse acceptance | small=actual hook point; candidates are post-filter observations",
        };

        foreach (var candidate in _grappleCandidates.Take(6))
        {
            var selected = ReflectionAccess.SameNativeObject(candidate.Attackable, selectedAttackable);
            lines.Add(
                $"  {(selected ? "G*" : "G ")} cost {Format(candidate.Cost)} | dist {Format(candidate.Distance)} | angle {Format(candidate.AngleDifference)} | input {Flag(candidate.HasInputDirection)}");
        }

        return new DebugSnapshot
        {
            ScreenWidth = width,
            ScreenHeight = height,
            Cursor = cursor,
            GrappleTarget = hasTarget && hasTargetMetrics ? target : null,
            EffectiveRadius = effectiveRadius,
            TargetMarkerRadius = targetMarkerRadius,
            PrecisionHit = precisionHit,
            GrappleState = grappleState,
            GrappleRangeCenter = grappleRangeCenter,
            NormalRangeRadiusX = normalRangeRadiusX,
            NormalRangeRadiusY = normalRangeRadiusY,
            RetainedRangeRadiusX = retainedRangeRadiusX,
            RetainedRangeRadiusY = retainedRangeRadiusY,
            Lines = lines,
            Markers = markers,
        };
    }

    private static string GetGrappleState(
        bool hasTarget,
        bool canLeash,
        bool precisionHit,
        double moveCooldown,
        double providerCooldown,
        string state)
    {
        if (IsPositive(moveCooldown) || IsPositive(providerCooldown))
        {
            return DiagnosticMarkerStates.Cooldown;
        }

        if (!string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase) && state != "n/a")
        {
            return DiagnosticMarkerStates.Busy;
        }

        if (hasTarget && !canLeash)
        {
            return DiagnosticMarkerStates.Blocked;
        }

        if (!hasTarget)
        {
            return DiagnosticMarkerStates.Candidate;
        }

        return precisionHit ? DiagnosticMarkerStates.Ready : DiagnosticMarkerStates.CursorMiss;
    }

    private static string GetGlobalBlockDetail(
        string grappleState,
        double moveCooldown,
        double providerCooldown,
        string state) => grappleState switch
        {
            DiagnosticMarkerStates.Cooldown => $"COOLDOWN M{Format(moveCooldown)} P{Format(providerCooldown)}",
            DiagnosticMarkerStates.Busy => $"STATE {state}",
            DiagnosticMarkerStates.Blocked => "CANLEASH NO",
            _ => string.Empty,
        };

    private static string GetCandidateState(
        GrappleCandidateObservation candidate,
        bool selected,
        bool retained,
        bool cursorHit,
        string grappleState,
        double normalRange,
        double retainedRange,
        double hookAngle,
        double noInputHookAngle,
        double retainAngleBonus)
    {
        var allowedRange = retained && double.IsFinite(retainedRange) ? retainedRange : normalRange;
        if (double.IsFinite(allowedRange) && candidate.Distance > allowedRange)
        {
            return DiagnosticMarkerStates.OutOfRange;
        }

        if (retained && double.IsFinite(normalRange) && candidate.Distance > normalRange)
        {
            return DiagnosticMarkerStates.RetainedRange;
        }

        if (selected)
        {
            return grappleState;
        }

        var angleLimit = candidate.HasInputDirection ? hookAngle : noInputHookAngle;
        if (retained && double.IsFinite(retainAngleBonus))
        {
            angleLimit += retainAngleBonus;
        }

        if (double.IsFinite(angleLimit) && candidate.AngleDifference > angleLimit)
        {
            return DiagnosticMarkerStates.Direction;
        }

        return cursorHit
            ? DiagnosticMarkerStates.SelectorConflict
            : DiagnosticMarkerStates.Candidate;
    }

    private static string GetCandidateDetail(
        GrappleCandidateObservation candidate,
        string candidateState,
        bool retained,
        double selectedCost,
        double effectiveRadius,
        ScreenPoint cursor,
        ScreenPoint target,
        double normalRange,
        double retainedRange,
        double hookAngle,
        double noInputHookAngle,
        double retainAngleBonus,
        string globalBlockDetail)
    {
        var cursorDistance = ScreenDistance(cursor, target);
        return candidateState switch
        {
            DiagnosticMarkerStates.Ready => $"READY d{Format(candidate.Distance)} a{Format(candidate.AngleDifference)}",
            DiagnosticMarkerStates.CursorMiss => $"CURSOR {Format(cursorDistance)} > {Format(effectiveRadius)}",
            DiagnosticMarkerStates.SelectorConflict => $"CURSOR HIT; COST {Format(candidate.Cost)} vs {Format(selectedCost)}",
            DiagnosticMarkerStates.Direction => $"ANGLE {Format(candidate.AngleDifference)} > {Format(GetAngleLimit(candidate, retained, hookAngle, noInputHookAngle, retainAngleBonus))}",
            DiagnosticMarkerStates.RetainedRange => $"RETAINED RANGE {Format(candidate.Distance)} / {Format(retainedRange)}",
            DiagnosticMarkerStates.OutOfRange => $"OUT OF RANGE {Format(candidate.Distance)} > {Format(retained && double.IsFinite(retainedRange) ? retainedRange : normalRange)}",
            DiagnosticMarkerStates.Cooldown or
            DiagnosticMarkerStates.Busy or
            DiagnosticMarkerStates.Blocked => globalBlockDetail,
            _ => $"COST {Format(candidate.Cost)} | d{Format(candidate.Distance)} a{Format(candidate.AngleDifference)}",
        };
    }

    private static double GetAngleLimit(
        GrappleCandidateObservation candidate,
        bool retained,
        double hookAngle,
        double noInputHookAngle,
        double retainAngleBonus) =>
        (candidate.HasInputDirection ? hookAngle : noInputHookAngle) +
        (retained && double.IsFinite(retainAngleBonus) ? retainAngleBonus : 0.0);

    private static double ScreenDistance(ScreenPoint first, ScreenPoint second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0.001;

    private bool IsPreciseGrappleTarget()
    {
        if (_types is null || _latestSpiritLeash is null)
        {
            return false;
        }

        if (!ReflectionAccess.GetBoolean(_latestSpiritLeash, false, "HasTarget") ||
            !ReflectionAccess.GetBoolean(_latestSpiritLeash, false, "CanLeash"))
        {
            return false;
        }

        if (!TryGetPrecisionMetrics(out var cursor, out var target, out var viewport, out _))
        {
            return false;
        }

        return PrecisionHitTest.IsHit(
            cursor,
            target,
            viewport,
            _settings.RadiusPixels,
            _settings.ReferenceHeight);
    }

    private bool TryGetPrecisionMetrics(
        out ScreenPoint cursor,
        out ScreenPoint target,
        out Viewport viewport,
        out double screenDistance)
    {
        cursor = default;
        target = default;
        viewport = default;
        screenDistance = double.NaN;
        if (_types is null || _latestSpiritLeash is null)
        {
            return false;
        }

        var targetLeash = ReflectionAccess.Get(_latestSpiritLeash, "m_targetLeash", "TargetLeash");
        var targetWorld = targetLeash is null
            ? null
            : TryInvoke(targetLeash, "GetAttackablePosition")
                ?? ReflectionAccess.Get(targetLeash, "SurfaceWorldPos");
        var mouseScreen = TryInvokeStatic(_types.MoonInput, "get_mousePosition");
        if (targetWorld is null || mouseScreen is null || !TryWorldToScreen(targetWorld, out target))
        {
            return false;
        }

        var width = Convert.ToInt32(TryInvokeStatic(_types.Screen, "get_width") ?? 0);
        var height = Convert.ToInt32(TryInvokeStatic(_types.Screen, "get_height") ?? 0);
        cursor = ReadPoint(mouseScreen);
        viewport = new Viewport(width, height);
        var deltaX = cursor.X - target.X;
        var deltaY = cursor.Y - target.Y;
        screenDistance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        return true;
    }

    private bool TryGetGrappleCandidateWorldPosition(object attackable, out object? worldPosition)
    {
        try
        {
            var hookTransform = ReflectionAccess.Get(attackable, "HookTargetTransform");
            worldPosition = hookTransform is null
                ? ReflectionAccess.Get(attackable, "Position") ?? TryInvoke(attackable, "get_Position")
                : ReflectionAccess.Get(hookTransform, "position", "Position") ?? TryInvoke(hookTransform, "get_position");
            return worldPosition is not null;
        }
        catch
        {
            worldPosition = null;
            return false;
        }
    }

    private bool TryGetBashWorldPosition(out object? worldPosition)
    {
        try
        {
            worldPosition = null;
            if (_latestBashTarget is not null)
            {
                worldPosition = ReflectionAccess.Get(_latestBashTarget, "Position")
                    ?? TryInvoke(_latestBashTarget, "get_Position");
            }

            worldPosition ??= _latestBashAttack is null
                ? null
                : ReflectionAccess.Get(_latestBashAttack, "m_playerTargetPosition", "PlayerTargetPosition");
            return _latestBashTarget is not null && worldPosition is not null;
        }
        catch
        {
            worldPosition = null;
            return false;
        }
    }

    private bool TryGetOriWorldPosition(out object? worldPosition)
    {
        worldPosition = null;
        if (_types is null)
        {
            return false;
        }

        var sein = ReflectionAccess.GetStatic(_types.Characters, "m_sein", "Sein");
        worldPosition = sein is null
            ? null
            : ReflectionAccess.Get(sein, "Position") ?? TryInvoke(sein, "get_Position");
        return worldPosition is not null;
    }

    private bool TryGetWorldRangeProjection(
        object worldOrigin,
        double range,
        out ScreenPoint center,
        out double radiusX,
        out double radiusY)
    {
        center = default;
        radiusX = 0.0;
        radiusY = 0.0;
        if (_types?.Vector3 is null || !double.IsFinite(range) || range <= 0.0)
        {
            return false;
        }

        try
        {
            var origin = ReadPoint(worldOrigin);
            var horizontal = ReflectionAccess.CreateVector(_types.Vector3, origin.X + range, origin.Y, origin.Depth);
            var vertical = ReflectionAccess.CreateVector(_types.Vector3, origin.X, origin.Y + range, origin.Depth);
            if (!TryWorldToScreen(worldOrigin, out center) ||
                !TryWorldToScreen(horizontal, out var horizontalScreen) ||
                !TryWorldToScreen(vertical, out var verticalScreen))
            {
                return false;
            }

            radiusX = ScreenDistance(center, horizontalScreen);
            radiusY = ScreenDistance(center, verticalScreen);
            return double.IsFinite(radiusX) && double.IsFinite(radiusY);
        }
        catch
        {
            center = default;
            radiusX = 0.0;
            radiusY = 0.0;
            return false;
        }
    }

    private bool TryWorldToScreen(object worldPosition, out ScreenPoint screenPosition)
    {
        screenPosition = default;
        var camera = GetMainCamera();
        var screen = camera is null ? null : TryInvoke(camera, "WorldToScreenPoint", worldPosition);
        if (screen is null)
        {
            return false;
        }

        screenPosition = ReadPoint(screen);
        return true;
    }

    private static double WorldDistance(object first, object second)
    {
        var firstPoint = ReadPoint(first);
        var secondPoint = ReadPoint(second);
        var deltaX = firstPoint.X - secondPoint.X;
        var deltaY = firstPoint.Y - secondPoint.Y;
        var deltaZ = firstPoint.Depth - secondPoint.Depth;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    private static double ReadNumber(object? instance, params string[] names) =>
        instance is null ? double.NaN : ReflectionAccess.ReadNumber(instance, names);

    private static string Format(double value, int decimals = 1) =>
        double.IsFinite(value) ? value.ToString($"F{decimals}") : "-";

    private static string Flag(bool value) => value ? "YES" : "NO";

    private void ApplyMouseAimToGameInput()
    {
        if (!_settings.Enabled || !_insideTargetSearch || !_hasAimDirection || _types is null)
        {
            return;
        }

        try
        {
            _originalHorizontal = ReflectionAccess.GetStatic(_types.CoreInput, "Horizontal");
            _originalVertical = ReflectionAccess.GetStatic(_types.CoreInput, "Vertical");
            if (_originalHorizontal is null || _originalVertical is null)
            {
                return;
            }

            ReflectionAccess.SetStatic(_types.CoreInput, (float)_aimX, "Horizontal");
            ReflectionAccess.SetStatic(_types.CoreInput, (float)_aimY, "Vertical");
            _gameInputOverridden = true;

            if (_settings.DebugLogging && !_loggedMouseInputOverride)
            {
                _loggedMouseInputOverride = true;
                _log.LogInfo("Mouse aim is overriding Core.Input.Horizontal/Vertical during Grapple target search.");
            }
        }
        catch (Exception exception)
        {
            RestoreGameInput();
            _log.LogError($"Could not override Grapple target input: {exception}");
        }
    }

    private void RestoreGameInput()
    {
        if (!_gameInputOverridden || _types is null)
        {
            return;
        }

        try
        {
            ReflectionAccess.SetStatic(_types.CoreInput, _originalHorizontal!, "Horizontal");
            ReflectionAccess.SetStatic(_types.CoreInput, _originalVertical!, "Vertical");
        }
        catch (Exception exception)
        {
            _log.LogError($"Could not restore the original game input: {exception}");
        }
        finally
        {
            _gameInputOverridden = false;
            _originalHorizontal = null;
            _originalVertical = null;
        }
    }

    private bool TryGetMouseAimDirection(out double x, out double y)
    {
        x = 0.0;
        y = 0.0;
        if (_types is null)
        {
            return false;
        }

        try
        {
            var hasSein = ReflectionAccess.GetStatic(_types.Characters, "HasSein");
            if (hasSein is bool hasCharacter && !hasCharacter)
            {
                return false;
            }

            var sein = ReflectionAccess.GetStatic(_types.Characters, "m_sein", "Sein");
            var camera = GetMainCamera();
            var mouse = TryInvokeStatic(_types.MoonInput, "get_mousePosition");
            var worldPosition = sein is null ? null : TryInvoke(sein, "get_Position");
            if (sein is null || camera is null || mouse is null || worldPosition is null)
            {
                return false;
            }

            var oriScreen = TryInvoke(camera, "WorldToScreenPoint", worldPosition);
            if (oriScreen is null)
            {
                return false;
            }

            x = ReflectionAccess.ReadNumber(mouse, "x", "X") - ReflectionAccess.ReadNumber(oriScreen, "x", "X");
            y = ReflectionAccess.ReadNumber(mouse, "y", "Y") - ReflectionAccess.ReadNumber(oriScreen, "y", "Y");
            var magnitude = Math.Sqrt((x * x) + (y * y));
            if (!double.IsFinite(magnitude) || magnitude < 0.001)
            {
                return false;
            }

            x /= magnitude;
            y /= magnitude;
            return true;
        }
        catch (Exception exception)
        {
            if (_settings.DebugLogging)
            {
                _log.LogWarning($"Could not calculate mouse aim direction: {exception.Message}");
            }

            return false;
        }
    }

    private object? GetMainCamera()
    {
        if (_types is null)
        {
            return null;
        }

        return TryInvokeStatic(_types.Camera, "get_main")
            ?? ReflectionAccess.GetStatic(_types.Camera, "main");
    }

    private void LogRouteChange(InputRoute route, bool preciseTarget)
    {
        if (!_settings.DebugLogging || route == _lastLoggedRoute)
        {
            return;
        }

        _lastLoggedRoute = route;
        _log.LogInfo($"Right-click route: {route}; precise Grapple target: {preciseTarget}.");
    }

    private static ScreenPoint ReadPoint(object vector) => new(
        ReflectionAccess.ReadNumber(vector, "x", "X"),
        ReflectionAccess.ReadNumber(vector, "y", "Y"),
        ReflectionAccess.ReadNumber(vector, "z", "Z"));

    private static object? TryInvoke(object instance, string methodName, params object?[] arguments)
    {
        try
        {
            return ReflectionAccess.Invoke(instance, methodName, arguments);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }

    private static object? TryInvokeStatic(Type type, string methodName, params object?[] arguments)
    {
        try
        {
            return ReflectionAccess.InvokeStatic(type, methodName, arguments);
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }
}

internal sealed class GrappleCandidateObservation
{
    public GrappleCandidateObservation(
        object attackable,
        double distance,
        double angleDifference,
        bool hasInputDirection,
        double cost)
    {
        Attackable = attackable;
        Distance = distance;
        AngleDifference = angleDifference;
        HasInputDirection = hasInputDirection;
        Cost = cost;
    }

    public object Attackable { get; }

    public double Distance { get; set; }

    public double AngleDifference { get; set; }

    public bool HasInputDirection { get; set; }

    public double Cost { get; set; }
}
