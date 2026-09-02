using System.Reflection;
using BepInEx.Logging;
using OriPrecisionGrapple.Core;

namespace OriPrecisionGrapple.Runtime;

internal sealed class DebugOverlayRenderer
{
    private const float PanelX = 12.0f;
    private const float PanelY = 12.0f;
    private const float PanelWidth = 610.0f;
    private const float LineHeight = 19.0f;

    private readonly Type _guiType;
    private readonly Type _rectType;
    private readonly Type _colorType;
    private readonly MethodInfo _box;
    private readonly MethodInfo _label;
    private readonly IRuntimeSettings _settings;
    private readonly ManualLogSource _log;
    private bool _failureLogged;

    private DebugOverlayRenderer(
        Type guiType,
        Type rectType,
        Type colorType,
        MethodInfo box,
        MethodInfo label,
        IRuntimeSettings settings,
        ManualLogSource log)
    {
        _guiType = guiType;
        _rectType = rectType;
        _colorType = colorType;
        _box = box;
        _label = label;
        _settings = settings;
        _log = log;
    }

    public static DebugOverlayRenderer? TryCreate(
        GameTypeCatalog types,
        IRuntimeSettings settings,
        ManualLogSource log)
    {
        if (types.Gui is null || types.Rect is null || types.Color is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var box = FindTextMethod(types.Gui, types.Rect, "Box", flags);
        var label = FindTextMethod(types.Gui, types.Rect, "Label", flags);
        return box is null || label is null
            ? null
            : new DebugOverlayRenderer(types.Gui, types.Rect, types.Color, box, label, settings, log);
    }

    public void Draw(DebugSnapshot snapshot)
    {
        if (!_settings.ShowOverlay || _failureLogged)
        {
            return;
        }

        object? originalColor = null;
        try
        {
            originalColor = ReflectionAccess.GetStatic(_guiType, "color");
            SetColor(1.0f, 1.0f, 1.0f, 1.0f);

            var panelHeight = 34.0f + (snapshot.Lines.Count * LineHeight);
            Box(PanelX, PanelY, PanelWidth, panelHeight, "Ori Precision Grapple Diagnostics");
            for (var index = 0; index < snapshot.Lines.Count; index++)
            {
                Label(
                    PanelX + 12.0f,
                    PanelY + 24.0f + (index * LineHeight),
                    PanelWidth - 24.0f,
                    LineHeight,
                    snapshot.Lines[index]);
            }

            if (_settings.ShowWorldMarkers)
            {
                DrawWorldMarkers(snapshot);
            }
        }
        catch (Exception exception)
        {
            _failureLogged = true;
            _log.LogError($"Diagnostics overlay disabled after a draw failure: {exception}");
        }
        finally
        {
            if (originalColor is not null)
            {
                try
                {
                    ReflectionAccess.SetStatic(_guiType, originalColor, "color");
                }
                catch
                {
                    // The original draw exception is the useful diagnostic.
                }
            }
        }
    }

    private void DrawWorldMarkers(DebugSnapshot snapshot)
    {
        if (snapshot.GrappleTarget is { } target && target.Depth > 0)
        {
            SetColor(
                snapshot.PrecisionHit ? 0.2f : 1.0f,
                snapshot.PrecisionHit ? 1.0f : 0.25f,
                0.25f,
                1.0f);
            DrawRadiusCircle(snapshot, target);
        }

        foreach (var marker in snapshot.Markers)
        {
            if (marker.Point.Depth <= 0)
            {
                continue;
            }

            switch (marker.Kind)
            {
                case DebugMarkerKind.GrappleTarget:
                    SetColor(0.2f, 1.0f, 0.3f, 1.0f);
                    break;
                case DebugMarkerKind.BashTarget:
                    SetColor(1.0f, 0.65f, 0.1f, 1.0f);
                    break;
                default:
                    SetColor(0.25f, 0.85f, 1.0f, 1.0f);
                    break;
            }

            var x = (float)marker.Point.X - 13.0f;
            var y = ToGuiY(snapshot, marker.Point) - 10.0f;
            Box(x, y, 27.0f, 21.0f, marker.Label);
        }

        SetColor(1.0f, 1.0f, 1.0f, 1.0f);
        Label(
            (float)snapshot.Cursor.X - 6.0f,
            ToGuiY(snapshot, snapshot.Cursor) - 10.0f,
            70.0f,
            20.0f,
            "+ mouse");
    }

    private void DrawRadiusCircle(DebugSnapshot snapshot, ScreenPoint target)
    {
        const int segments = 40;
        var radius = snapshot.EffectiveRadius;
        for (var index = 0; index < segments; index++)
        {
            var angle = (Math.PI * 2.0 * index) / segments;
            var x = target.X + (Math.Cos(angle) * radius);
            var y = target.Y + (Math.Sin(angle) * radius);
            Box((float)x - 1.5f, (float)(snapshot.ScreenHeight - y) - 1.5f, 3.0f, 3.0f, string.Empty);
        }
    }

    private void SetColor(float red, float green, float blue, float alpha)
    {
        var color = Activator.CreateInstance(_colorType, red, green, blue, alpha)
            ?? throw new InvalidOperationException("Could not construct UnityEngine.Color.");
        ReflectionAccess.SetStatic(_guiType, color, "color");
    }

    private void Box(float x, float y, float width, float height, string text) =>
        _box.Invoke(null, new[] { CreateRect(x, y, width, height), text });

    private void Label(float x, float y, float width, float height, string text) =>
        _label.Invoke(null, new[] { CreateRect(x, y, width, height), text });

    private object CreateRect(float x, float y, float width, float height) =>
        Activator.CreateInstance(_rectType, x, y, width, height)
        ?? throw new InvalidOperationException("Could not construct UnityEngine.Rect.");

    private static float ToGuiY(DebugSnapshot snapshot, ScreenPoint point) =>
        (float)(snapshot.ScreenHeight - point.Y);

    private static MethodInfo? FindTextMethod(Type guiType, Type rectType, string name, BindingFlags flags) =>
        guiType.GetMethods(flags)
            .FirstOrDefault(method =>
            {
                if (method.Name != name)
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == rectType &&
                    parameters[1].ParameterType == typeof(string);
            });
}
