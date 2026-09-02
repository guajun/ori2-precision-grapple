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
    private readonly Type? _matrixType;
    private readonly MethodInfo _drawTexture;
    private readonly MethodInfo _label;
    private readonly object _whiteTexture;
    private readonly object _textStyle;
    private readonly IRuntimeSettings _settings;
    private readonly ManualLogSource _log;
    private bool _failureLogged;
    private bool _successLogged;

    private DebugOverlayRenderer(
        Type guiType,
        Type rectType,
        Type colorType,
        Type? matrixType,
        MethodInfo drawTexture,
        MethodInfo label,
        object whiteTexture,
        object textStyle,
        IRuntimeSettings settings,
        ManualLogSource log)
    {
        _guiType = guiType;
        _rectType = rectType;
        _colorType = colorType;
        _matrixType = matrixType;
        _drawTexture = drawTexture;
        _label = label;
        _whiteTexture = whiteTexture;
        _textStyle = textStyle;
        _settings = settings;
        _log = log;
    }

    public static DebugOverlayRenderer? TryCreate(
        GameTypeCatalog types,
        IRuntimeSettings settings,
        ManualLogSource log,
        out string status)
    {
        if (types.Gui is null || types.Rect is null || types.Color is null || types.GuiStyle is null || types.Texture2D is null)
        {
            status = $"Missing GUI types: GUI={types.Gui is not null}, Rect={types.Rect is not null}, Color={types.Color is not null}, GUIStyle={types.GuiStyle is not null}, Texture2D={types.Texture2D is not null}.";
            return null;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var drawTexture = types.Gui.GetMethods(flags)
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "DrawTexture" &&
                    parameters.Length == 2 &&
                    parameters[0].ParameterType == types.Rect &&
                    parameters[1].ParameterType.FullName == "UnityEngine.Texture";
            });
        var label = FindStyledTextMethod(types.Gui, types.Rect, types.GuiStyle, flags);
        var whiteTexture = ReflectionAccess.GetStatic(types.Texture2D, "whiteTexture");
        var textStyle = Activator.CreateInstance(types.GuiStyle);
        if (drawTexture is null || label is null || whiteTexture is null || textStyle is null)
        {
            status = $"Explicit draw resources unavailable: DrawTexture={drawTexture is not null}, StyledLabel={label is not null}, WhiteTexture={whiteTexture is not null}, Style={textStyle is not null}.";
            return null;
        }

        ReflectionAccess.TrySet(textStyle, 15, "fontSize");
        ReflectionAccess.TrySet(textStyle, false, "wordWrap");
        ReflectionAccess.TrySet(textStyle, false, "richText");
        var normal = ReflectionAccess.Get(textStyle, "normal");
        if (normal is not null)
        {
            var white = Activator.CreateInstance(types.Color, 1.0f, 1.0f, 1.0f, 1.0f);
            if (white is not null)
            {
                ReflectionAccess.TrySet(normal, white, "textColor");
            }
        }

        status = "Independent GUIStyle and direct texture drawing resources resolved.";
        return new DebugOverlayRenderer(
            types.Gui,
            types.Rect,
            types.Color,
            types.Matrix4x4,
            drawTexture,
            label,
            whiteTexture,
            textStyle,
            settings,
            log);
    }

    public void Draw(DebugSnapshot snapshot)
    {
        if (!_settings.ShowOverlay || _failureLogged)
        {
            return;
        }

        object? originalColor = null;
        object? originalContentColor = null;
        object? originalBackgroundColor = null;
        object? originalEnabled = null;
        object? originalDepth = null;
        object? originalMatrix = null;
        try
        {
            originalColor = ReflectionAccess.GetStatic(_guiType, "color");
            originalContentColor = ReflectionAccess.GetStatic(_guiType, "contentColor");
            originalBackgroundColor = ReflectionAccess.GetStatic(_guiType, "backgroundColor");
            originalEnabled = ReflectionAccess.GetStatic(_guiType, "enabled");
            originalDepth = ReflectionAccess.GetStatic(_guiType, "depth");
            originalMatrix = ReflectionAccess.GetStatic(_guiType, "matrix");

            ReflectionAccess.TrySetStatic(_guiType, true, "enabled");
            ReflectionAccess.TrySetStatic(_guiType, -10000, "depth");
            if (_matrixType is not null)
            {
                var identity = ReflectionAccess.GetStatic(_matrixType, "identity");
                if (identity is not null)
                {
                    ReflectionAccess.TrySetStatic(_guiType, identity, "matrix");
                }
            }

            SetColor(1.0f, 1.0f, 1.0f, 1.0f);
            SetGuiColor("contentColor", 1.0f, 1.0f, 1.0f, 1.0f);
            SetGuiColor("backgroundColor", 0.12f, 0.12f, 0.12f, 0.95f);

            var panelHeight = 34.0f + (snapshot.Lines.Count * LineHeight);
            Fill(PanelX, PanelY, PanelWidth, panelHeight, 0.05f, 0.05f, 0.05f, 0.88f);
            Label(PanelX + 12.0f, PanelY + 3.0f, PanelWidth - 24.0f, LineHeight, "Ori Precision Grapple Diagnostics");
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

            if (!_successLogged)
            {
                _successLogged = true;
                _log.LogInfo("Diagnostics overlay completed its first OnGUI draw.");
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
                    Restore(originalContentColor, "contentColor");
                    Restore(originalBackgroundColor, "backgroundColor");
                    Restore(originalEnabled, "enabled");
                    Restore(originalDepth, "depth");
                    Restore(originalMatrix, "matrix");
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
            Fill(x, y, 27.0f, 21.0f);
            SetColor(1.0f, 1.0f, 1.0f, 1.0f);
            Label(x + 4.0f, y + 1.0f, 25.0f, 20.0f, marker.Label);
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
            Fill((float)x - 1.5f, (float)(snapshot.ScreenHeight - y) - 1.5f, 3.0f, 3.0f);
        }
    }

    private void SetColor(float red, float green, float blue, float alpha)
    {
        SetGuiColor("color", red, green, blue, alpha);
    }

    private void SetGuiColor(string property, float red, float green, float blue, float alpha)
    {
        var color = Activator.CreateInstance(_colorType, red, green, blue, alpha)
            ?? throw new InvalidOperationException("Could not construct UnityEngine.Color.");
        ReflectionAccess.TrySetStatic(_guiType, color, property);
    }

    private void Restore(object? value, string property)
    {
        if (value is not null)
        {
            ReflectionAccess.TrySetStatic(_guiType, value, property);
        }
    }

    private void Fill(float x, float y, float width, float height) =>
        _drawTexture.Invoke(null, new[] { CreateRect(x, y, width, height), _whiteTexture });

    private void Fill(float x, float y, float width, float height, float red, float green, float blue, float alpha)
    {
        SetColor(red, green, blue, alpha);
        Fill(x, y, width, height);
        SetColor(1.0f, 1.0f, 1.0f, 1.0f);
    }

    private void Label(float x, float y, float width, float height, string text) =>
        _label.Invoke(null, new[] { CreateRect(x, y, width, height), text, _textStyle });

    private object CreateRect(float x, float y, float width, float height) =>
        Activator.CreateInstance(_rectType, x, y, width, height)
        ?? throw new InvalidOperationException("Could not construct UnityEngine.Rect.");

    private static float ToGuiY(DebugSnapshot snapshot, ScreenPoint point) =>
        (float)(snapshot.ScreenHeight - point.Y);

    private static MethodInfo? FindStyledTextMethod(Type guiType, Type rectType, Type styleType, BindingFlags flags) =>
        guiType.GetMethods(flags)
            .FirstOrDefault(method =>
            {
                if (method.Name != "Label")
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 3 &&
                    parameters[0].ParameterType == rectType &&
                    parameters[1].ParameterType == typeof(string) &&
                    parameters[2].ParameterType == styleType;
            });
}
