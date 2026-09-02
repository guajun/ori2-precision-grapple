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
        if (!_settings.ShowOverlay && !_settings.ExternalMonitorEnabled)
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
            _diagnosticPipe?.Publish(ToDiagnosticFrame(snapshot));
            if (!_settings.ShowOverlay)
            {
                return;
            }

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

    private static DiagnosticFrame ToDiagnosticFrame(DebugSnapshot snapshot) => new()
    {
        ScreenWidth = snapshot.ScreenWidth,
        ScreenHeight = snapshot.ScreenHeight,
        CursorX = snapshot.Cursor.X,
        CursorY = snapshot.Cursor.Y,
        EffectiveRadius = snapshot.EffectiveRadius,
        PrecisionHit = snapshot.PrecisionHit,
        GrappleTargetX = snapshot.GrappleTarget?.X,
        GrappleTargetY = snapshot.GrappleTarget?.Y,
        Lines = snapshot.Lines.ToArray(),
        Markers = snapshot.Markers.Select(marker => new DiagnosticMarker
        {
            Label = marker.Label,
            Kind = marker.Kind.ToString(),
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

        var markers = new List<DebugMarker>();
        if (hasTarget && hasTargetMetrics)
        {
            markers.Add(new DebugMarker(target, "G*", DebugMarkerKind.GrappleTarget));
        }

        var candidateIndex = 0;
        foreach (var candidate in _grappleCandidates)
        {
            if (!TryGetGrappleCandidateWorldPosition(candidate.Attackable, out var worldPosition) ||
                !TryWorldToScreen(worldPosition!, out var screenPosition))
            {
                continue;
            }

            candidateIndex++;
            markers.Add(new DebugMarker(
                screenPosition,
                $"G{candidateIndex}",
                DebugMarkerKind.GrappleCandidate));
        }

        var bashRange = ReadNumber(_latestBashAttack, "Range");
        var bashDistance = double.NaN;
        var hasBashTarget = TryGetBashWorldPosition(out var bashWorld);
        if (hasBashTarget)
        {
            if (TryWorldToScreen(bashWorld!, out var bashScreen))
            {
                markers.Add(new DebugMarker(bashScreen, "B", DebugMarkerKind.BashTarget));
            }

            if (TryGetOriWorldPosition(out var oriWorld))
            {
                bashDistance = WorldDistance(oriWorld!, bashWorld!);
            }
        }

        var playerInput = ReflectionAccess.GetStatic(_types.PlayerInput, "Instance");
        var playerInputActive = playerInput is not null && ReflectionAccess.GetBoolean(playerInput, false, "Active");
        var moveCooldownTimer = ReadNumber(_latestSpiritLeash, "MoveCooldownTimer", "_MoveCooldownTimer_k__BackingField");
        var providerCooldownTimer = ReadNumber(_latestSpiritLeash, "ProviderCooldownTimer", "_ProviderCooldownTimer_k__BackingField");
        var normalRange = ReadNumber(_latestSpiritLeash, "SpiritLeashRange");
        var currentRange = ReadNumber(_latestSpiritLeash, "SpiritLeashRangeCurrentTarget");
        var targetDistance = ReadNumber(_latestSpiritLeash, "DistanceFromOri");
        var state = _latestSpiritLeash is null
            ? "n/a"
            : ReflectionAccess.Get(_latestSpiritLeash, "m_currentState")?.ToString() ?? "n/a";
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
            $"Grapple candidates {_grappleCandidates.Count} | screen-visible {candidateIndex}",
            $"Target/cursor delta {Format(targetScreenDistance)} px | threshold {Format(effectiveRadius)} px | precision {Flag(precisionHit)}",
            $"Bash target {Flag(hasBashTarget)} | distance {Format(bashDistance)} | range {Format(bashRange)} | in range {Flag(bashInRange)}",
            "Markers: G*=selected Grapple, G#=evaluated Grapple, B=Bash, ring=precision radius",
        };

        foreach (var candidate in _grappleCandidates.Take(6))
        {
            lines.Add(
                $"  G cost {Format(candidate.Cost)} | dist {Format(candidate.Distance)} | angle {Format(candidate.AngleDifference)} | input {Flag(candidate.HasInputDirection)}");
        }

        return new DebugSnapshot
        {
            ScreenWidth = width,
            ScreenHeight = height,
            Cursor = cursor,
            GrappleTarget = hasTarget && hasTargetMetrics ? target : null,
            EffectiveRadius = effectiveRadius,
            PrecisionHit = precisionHit,
            Lines = lines,
            Markers = markers,
        };
    }

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
