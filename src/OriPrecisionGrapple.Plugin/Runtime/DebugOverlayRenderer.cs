using System.Reflection;
using BepInEx.Logging;
using OriPrecisionGrapple.Core;

namespace OriPrecisionGrapple.Runtime;

internal sealed class DebugOverlayRenderer
{
    private const int MarkerPoolSize = 80;

    private readonly Type _gameObjectType;
    private readonly Type _vector2Type;
    private readonly Type _vector3Type;
    private readonly Type _colorType;
    private readonly MethodInfo _addGuiText;
    private readonly object _font;
    private readonly object _rootTransform;
    private readonly IRuntimeSettings _settings;
    private readonly ManualLogSource _log;
    private readonly ScreenText _hudShadow;
    private readonly ScreenText _hud;
    private readonly List<ScreenText> _markers = new();
    private bool _failureLogged;
    private bool _successLogged;

    private DebugOverlayRenderer(
        GameTypeCatalog types,
        object font,
        object rootTransform,
        MethodInfo addGuiText,
        IRuntimeSettings settings,
        ManualLogSource log)
    {
        _gameObjectType = types.GameObject!;
        _vector2Type = types.Vector2!;
        _vector3Type = types.Vector3!;
        _colorType = types.Color!;
        _addGuiText = addGuiText;
        _font = font;
        _rootTransform = rootTransform;
        _settings = settings;
        _log = log;

        var upperLeft = Enum.Parse(types.TextAnchor!, "UpperLeft");
        _hudShadow = CreateText(
            "Diagnostics Shadow",
            15,
            upperLeft,
            CreateColor(0.0f, 0.0f, 0.0f, 0.95f));
        _hud = CreateText(
            "Diagnostics Text",
            15,
            upperLeft,
            CreateColor(1.0f, 1.0f, 1.0f, 1.0f));

        SetViewportPosition(_hudShadow, 0.0, 1.0);
        SetViewportPosition(_hud, 0.0, 1.0);
        ReflectionAccess.TrySet(_hudShadow.Component, CreateVector2(14.0, -14.0), "pixelOffset");
        ReflectionAccess.TrySet(_hud.Component, CreateVector2(12.0, -12.0), "pixelOffset");

        var middleCenter = Enum.Parse(types.TextAnchor!, "MiddleCenter");
        for (var index = 0; index < MarkerPoolSize; index++)
        {
            var marker = CreateText(
                $"Marker {index}",
                18,
                middleCenter,
                CreateColor(1.0f, 1.0f, 1.0f, 1.0f));
            SetEnabled(marker, false);
            _markers.Add(marker);
        }
    }

    public static DebugOverlayRenderer? TryCreate(
        GameTypeCatalog types,
        IRuntimeSettings settings,
        ManualLogSource log,
        out string status)
    {
        var required = new Dictionary<string, Type?>
        {
            ["GameObject"] = types.GameObject,
            ["Object"] = types.UnityObject,
            ["Resources"] = types.Resources,
            ["GUIText"] = types.GuiText,
            ["Font"] = types.Font,
            ["Vector2"] = types.Vector2,
            ["Vector3"] = types.Vector3,
            ["Color"] = types.Color,
            ["TextAnchor"] = types.TextAnchor,
        };
        var missing = required.Where(item => item.Value is null).Select(item => item.Key).ToArray();
        if (missing.Length > 0)
        {
            status = $"Missing Unity screen-renderer types: {string.Join(", ", missing)}.";
            return null;
        }

        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var addGuiText = types.GameObject!.GetMethods(flags)
                .FirstOrDefault(method =>
                    method.Name == "AddComponent" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 0)
                ?.MakeGenericMethod(types.GuiText!);
            var getFont = types.Resources!.GetMethods(flags)
                .FirstOrDefault(method =>
                    method.Name == "GetBuiltinResource" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 1)
                ?.MakeGenericMethod(types.Font!);
            if (addGuiText is null || getFont is null)
            {
                status = $"Required generic methods unavailable: AddComponent={addGuiText is not null}, GetBuiltinResource={getFont is not null}.";
                return null;
            }

            var font = getFont.Invoke(null, new object[] { "Arial.ttf" });
            if (font is null)
            {
                status = "Unity did not return the built-in Arial font.";
                return null;
            }

            var root = Activator.CreateInstance(types.GameObject!, "Ori Precision Grapple Diagnostics");
            var rootTransform = root is null ? null : ReflectionAccess.Get(root, "transform");
            if (root is null || rootTransform is null)
            {
                status = "Could not create the diagnostics root GameObject.";
                return null;
            }

            ReflectionAccess.InvokeStatic(types.UnityObject!, "DontDestroyOnLoad", root);
            status = "Persistent GUIText root, built-in font and marker pool created.";
            return new DebugOverlayRenderer(types, font, rootTransform, addGuiText, settings, log);
        }
        catch (Exception exception)
        {
            status = $"GUIText initialization failed: {Unwrap(exception).Message}";
            return null;
        }
    }

    public void Draw(DebugSnapshot snapshot)
    {
        if (!_settings.ShowOverlay || _failureLogged)
        {
            return;
        }

        try
        {
            var text = string.Join("\n", snapshot.Lines);
            ReflectionAccess.TrySet(_hudShadow.Component, text, "text");
            ReflectionAccess.TrySet(_hud.Component, text, "text");
            SetEnabled(_hudShadow, true);
            SetEnabled(_hud, true);

            var markerIndex = 0;
            if (_settings.ShowWorldMarkers && snapshot.ScreenWidth > 0 && snapshot.ScreenHeight > 0)
            {
                if (snapshot.GrappleTarget is { } target && target.Depth > 0)
                {
                    const int segments = 40;
                    for (var index = 0; index < segments && markerIndex < _markers.Count; index++)
                    {
                        var angle = (Math.PI * 2.0 * index) / segments;
                        var x = target.X + (Math.Cos(angle) * snapshot.EffectiveRadius);
                        var y = target.Y + (Math.Sin(angle) * snapshot.EffectiveRadius);
                        var color = snapshot.PrecisionHit
                            ? CreateColor(0.2f, 1.0f, 0.3f, 1.0f)
                            : CreateColor(1.0f, 0.25f, 0.25f, 1.0f);
                        UpdateMarker(markerIndex++, ".", x, y, color, snapshot);
                    }
                }

                foreach (var marker in snapshot.Markers)
                {
                    if (markerIndex >= _markers.Count || marker.Point.Depth <= 0)
                    {
                        continue;
                    }

                    var color = marker.Kind switch
                    {
                        DebugMarkerKind.GrappleTarget => CreateColor(0.2f, 1.0f, 0.3f, 1.0f),
                        DebugMarkerKind.BashTarget => CreateColor(1.0f, 0.65f, 0.1f, 1.0f),
                        _ => CreateColor(0.25f, 0.85f, 1.0f, 1.0f),
                    };
                    UpdateMarker(markerIndex++, marker.Label, marker.Point.X, marker.Point.Y, color, snapshot);
                }

                if (markerIndex < _markers.Count &&
                    double.IsFinite(snapshot.Cursor.X) &&
                    double.IsFinite(snapshot.Cursor.Y))
                {
                    UpdateMarker(
                        markerIndex++,
                        "+",
                        snapshot.Cursor.X,
                        snapshot.Cursor.Y,
                        CreateColor(1.0f, 1.0f, 1.0f, 1.0f),
                        snapshot);
                }
            }

            for (; markerIndex < _markers.Count; markerIndex++)
            {
                SetEnabled(_markers[markerIndex], false);
            }

            if (!_successLogged)
            {
                _successLogged = true;
                _log.LogInfo("Diagnostics GUIText overlay completed its first screen update.");
            }
        }
        catch (Exception exception)
        {
            _failureLogged = true;
            _log.LogError($"Diagnostics GUIText overlay disabled after an update failure: {Unwrap(exception)}");
        }
    }

    private ScreenText CreateText(string name, int fontSize, object anchor, object color)
    {
        var gameObject = Activator.CreateInstance(_gameObjectType, name)
            ?? throw new InvalidOperationException($"Could not create GameObject '{name}'.");
        var transform = ReflectionAccess.Get(gameObject, "transform")
            ?? throw new InvalidOperationException($"GameObject '{name}' has no transform.");
        ReflectionAccess.Invoke(transform, "SetParent", _rootTransform, false);
        var component = _addGuiText.Invoke(gameObject, null)
            ?? throw new InvalidOperationException($"Could not add GUIText to '{name}'.");

        ReflectionAccess.TrySet(component, _font, "font");
        ReflectionAccess.TrySet(component, fontSize, "fontSize");
        ReflectionAccess.TrySet(component, anchor, "anchor");
        ReflectionAccess.TrySet(component, color, "color");
        ReflectionAccess.TrySet(component, false, "richText");
        ReflectionAccess.TrySet(component, 1.0f, "lineSpacing");
        return new ScreenText(gameObject, transform, component);
    }

    private void UpdateMarker(
        int index,
        string text,
        double screenX,
        double screenY,
        object color,
        DebugSnapshot snapshot)
    {
        var marker = _markers[index];
        ReflectionAccess.TrySet(marker.Component, text, "text");
        ReflectionAccess.TrySet(marker.Component, color, "color");
        SetViewportPosition(
            marker,
            screenX / snapshot.ScreenWidth,
            screenY / snapshot.ScreenHeight);
        SetEnabled(marker, true);
    }

    private void SetViewportPosition(ScreenText text, double x, double y)
    {
        var position = ReflectionAccess.CreateVector(_vector3Type, x, y, 0.0);
        ReflectionAccess.TrySet(text.Transform, position, "position");
    }

    private object CreateVector2(double x, double y) =>
        ReflectionAccess.CreateVector(_vector2Type, x, y, 0.0);

    private object CreateColor(float red, float green, float blue, float alpha) =>
        Activator.CreateInstance(_colorType, red, green, blue, alpha)
        ?? throw new InvalidOperationException("Could not construct UnityEngine.Color.");

    private static void SetEnabled(ScreenText text, bool enabled)
    {
        if (!ReflectionAccess.TrySet(text.Component, enabled, "enabled"))
        {
            ReflectionAccess.Invoke(text.GameObject, "SetActive", enabled);
        }
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: not null }
            ? exception.InnerException
            : exception;

    private sealed class ScreenText
    {
        public ScreenText(object gameObject, object transform, object component)
        {
            GameObject = gameObject;
            Transform = transform;
            Component = component;
        }

        public object GameObject { get; }

        public object Transform { get; }

        public object Component { get; }
    }
}
