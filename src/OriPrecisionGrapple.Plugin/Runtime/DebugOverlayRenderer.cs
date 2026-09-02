using System.Reflection;
using BepInEx.Logging;
using OriPrecisionGrapple.Core.Diagnostics;

namespace OriPrecisionGrapple.Runtime;

internal sealed class DebugOverlayRenderer
{
    private const int MarkerPoolSize = 80;
    private const float PanelWidth = 640.0f;
    private const float PanelHeight = 410.0f;

    private readonly Type _gameObjectType;
    private readonly Type _vector2Type;
    private readonly Type _colorType;
    private readonly Type _horizontalWrapModeType;
    private readonly Type _verticalWrapModeType;
    private readonly MethodInfo _addText;
    private readonly object _font;
    private readonly object _rootTransform;
    private readonly IRuntimeSettings _settings;
    private readonly ManualLogSource _log;
    private readonly ScreenGraphic _panel;
    private readonly ScreenGraphic _hudShadow;
    private readonly ScreenGraphic _hud;
    private readonly List<ScreenGraphic> _markers = new();
    private bool _failureLogged;
    private bool _successLogged;

    private DebugOverlayRenderer(
        GameTypeCatalog types,
        object font,
        object rootTransform,
        MethodInfo addText,
        MethodInfo addImage,
        IRuntimeSettings settings,
        ManualLogSource log)
    {
        _gameObjectType = types.GameObject!;
        _vector2Type = types.Vector2!;
        _colorType = types.Color!;
        _horizontalWrapModeType = types.HorizontalWrapMode!;
        _verticalWrapModeType = types.VerticalWrapMode!;
        _addText = addText;
        _font = font;
        _rootTransform = rootTransform;
        _settings = settings;
        _log = log;

        _panel = CreateGraphic("Diagnostics Panel", addImage);
        ConfigureRect(
            _panel.RectTransform,
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(12.0, -12.0),
            CreateVector2(PanelWidth, PanelHeight));
        ReflectionAccess.TrySet(_panel.Component, CreateColor(0.03f, 0.03f, 0.03f, 0.82f), "color");
        ReflectionAccess.TrySet(_panel.Component, false, "raycastTarget");

        var upperLeft = Enum.Parse(types.TextAnchor!, "UpperLeft");
        _hudShadow = CreateText("Diagnostics Shadow", 15, upperLeft, CreateColor(0.0f, 0.0f, 0.0f, 1.0f));
        ConfigureRect(
            _hudShadow.RectTransform,
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(25.0, -25.0),
            CreateVector2(PanelWidth - 24.0, PanelHeight - 24.0));

        _hud = CreateText("Diagnostics Text", 15, upperLeft, CreateColor(1.0f, 1.0f, 1.0f, 1.0f));
        ConfigureRect(
            _hud.RectTransform,
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(0.0, 1.0),
            CreateVector2(24.0, -24.0),
            CreateVector2(PanelWidth - 24.0, PanelHeight - 24.0));

        var middleCenter = Enum.Parse(types.TextAnchor!, "MiddleCenter");
        for (var index = 0; index < MarkerPoolSize; index++)
        {
            var marker = CreateText(
                $"Marker {index}",
                18,
                middleCenter,
                CreateColor(1.0f, 1.0f, 1.0f, 1.0f));
            ConfigureRect(
                marker.RectTransform,
                CreateVector2(0.0, 0.0),
                CreateVector2(0.0, 0.0),
                CreateVector2(0.5, 0.5),
                CreateVector2(0.0, 0.0),
                CreateVector2(58.0, 28.0));
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
            ["Font"] = types.Font,
            ["Vector2"] = types.Vector2,
            ["Color"] = types.Color,
            ["TextAnchor"] = types.TextAnchor,
            ["Canvas"] = types.Canvas,
            ["RenderMode"] = types.RenderMode,
            ["UI.Text"] = types.UiText,
            ["UI.Image"] = types.UiImage,
            ["HorizontalWrapMode"] = types.HorizontalWrapMode,
            ["VerticalWrapMode"] = types.VerticalWrapMode,
        };
        var missing = required.Where(item => item.Value is null).Select(item => item.Key).ToArray();
        if (missing.Length > 0)
        {
            status = $"Missing Unity Canvas types: {string.Join(", ", missing)}.";
            return null;
        }

        try
        {
            var addCanvas = FindGenericAddComponent(types.GameObject!, types.Canvas!);
            var addText = FindGenericAddComponent(types.GameObject!, types.UiText!);
            var addImage = FindGenericAddComponent(types.GameObject!, types.UiImage!);
            var getFont = FindGenericBuiltinResource(types.Resources!, types.Font!);
            if (addCanvas is null || addText is null || addImage is null || getFont is null)
            {
                status = $"Required Canvas methods unavailable: Canvas={addCanvas is not null}, Text={addText is not null}, Image={addImage is not null}, Font={getFont is not null}.";
                return null;
            }

            var font = getFont.Invoke(null, new object[] { "Arial.ttf" });
            if (font is null)
            {
                status = "Unity did not return the built-in Arial font.";
                return null;
            }

            var root = Activator.CreateInstance(types.GameObject!, "Ori Precision Grapple Diagnostics Canvas");
            var rootTransform = root is null ? null : ReflectionAccess.Get(root, "transform");
            var canvas = root is null ? null : addCanvas.Invoke(root, null);
            if (root is null || rootTransform is null || canvas is null)
            {
                status = "Could not create the diagnostics Canvas GameObject.";
                return null;
            }

            ReflectionAccess.TrySet(canvas, Enum.Parse(types.RenderMode!, "ScreenSpaceOverlay"), "renderMode");
            ReflectionAccess.TrySet(canvas, true, "overrideSorting");
            ReflectionAccess.TrySet(canvas, 32767, "sortingOrder");
            ReflectionAccess.TrySet(canvas, false, "pixelPerfect");
            ReflectionAccess.InvokeStatic(types.UnityObject!, "DontDestroyOnLoad", root);

            status = "ScreenSpaceOverlay Canvas, built-in font, panel and marker pool created.";
            return new DebugOverlayRenderer(types, font, rootTransform, addText, addImage, settings, log);
        }
        catch (Exception exception)
        {
            status = $"Canvas initialization failed: {Unwrap(exception).Message}";
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
            SetEnabled(_panel, true);
            SetEnabled(_hudShadow, true);
            SetEnabled(_hud, true);

            var markerIndex = 0;
            if (_settings.ShowWorldMarkers && snapshot.ScreenWidth > 0 && snapshot.ScreenHeight > 0)
            {
                if (double.IsFinite(snapshot.Cursor.X) && double.IsFinite(snapshot.Cursor.Y))
                {
                    const int segments = 40;
                    for (var index = 0; index < segments && markerIndex < _markers.Count; index++)
                    {
                        var angle = (Math.PI * 2.0 * index) / segments;
                        var x = snapshot.Cursor.X + (Math.Cos(angle) * snapshot.EffectiveRadius);
                        var y = snapshot.Cursor.Y + (Math.Sin(angle) * snapshot.EffectiveRadius);
                        var color = GetMarkerColor(snapshot.GrappleState);
                        UpdateMarker(markerIndex++, ".", x, y, color);
                    }
                }

                foreach (var marker in snapshot.Markers)
                {
                    if (markerIndex >= _markers.Count || marker.Point.Depth <= 0)
                    {
                        continue;
                    }

                    var color = marker.Kind == DebugMarkerKind.BashTarget
                        ? CreateColor(1.0f, 0.65f, 0.1f, 1.0f)
                        : GetMarkerColor(marker.State);
                    UpdateMarker(markerIndex++, marker.Label, marker.Point.X, marker.Point.Y, color);
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
                        CreateColor(1.0f, 1.0f, 1.0f, 1.0f));
                }
            }

            for (; markerIndex < _markers.Count; markerIndex++)
            {
                SetEnabled(_markers[markerIndex], false);
            }

            if (!_successLogged)
            {
                _successLogged = true;
                _log.LogInfo("Diagnostics Canvas completed its first screen update.");
            }
        }
        catch (Exception exception)
        {
            _failureLogged = true;
            _log.LogError($"Diagnostics Canvas disabled after an update failure: {Unwrap(exception)}");
        }
    }

    private ScreenGraphic CreateText(string name, int fontSize, object alignment, object color)
    {
        var graphic = CreateGraphic(name, _addText);
        ReflectionAccess.TrySet(graphic.Component, _font, "font");
        ReflectionAccess.TrySet(graphic.Component, fontSize, "fontSize");
        ReflectionAccess.TrySet(graphic.Component, alignment, "alignment");
        ReflectionAccess.TrySet(graphic.Component, color, "color");
        ReflectionAccess.TrySet(graphic.Component, false, "raycastTarget");
        ReflectionAccess.TrySet(graphic.Component, false, "supportRichText");
        ReflectionAccess.TrySet(graphic.Component, 1.0f, "lineSpacing");
        ReflectionAccess.TrySet(graphic.Component, Enum.Parse(_horizontalWrapModeType, "Overflow"), "horizontalOverflow");
        ReflectionAccess.TrySet(graphic.Component, Enum.Parse(_verticalWrapModeType, "Overflow"), "verticalOverflow");
        return graphic;
    }

    private ScreenGraphic CreateGraphic(string name, MethodInfo addComponent)
    {
        var gameObject = Activator.CreateInstance(_gameObjectType, name)
            ?? throw new InvalidOperationException($"Could not create GameObject '{name}'.");
        var component = addComponent.Invoke(gameObject, null)
            ?? throw new InvalidOperationException($"Could not add a UI component to '{name}'.");
        var rectTransform = ReflectionAccess.Get(component, "rectTransform")
            ?? throw new InvalidOperationException($"UI component '{name}' has no RectTransform.");
        ReflectionAccess.Invoke(rectTransform, "SetParent", _rootTransform, false);
        return new ScreenGraphic(gameObject, rectTransform, component);
    }

    private void UpdateMarker(int index, string text, double screenX, double screenY, object color)
    {
        var marker = _markers[index];
        ReflectionAccess.TrySet(marker.Component, text, "text");
        ReflectionAccess.TrySet(marker.Component, color, "color");
        ReflectionAccess.TrySet(marker.RectTransform, CreateVector2(screenX, screenY), "anchoredPosition");
        SetEnabled(marker, true);
    }

    private static void ConfigureRect(
        object rectTransform,
        object anchorMin,
        object anchorMax,
        object pivot,
        object position,
        object size)
    {
        ReflectionAccess.TrySet(rectTransform, anchorMin, "anchorMin");
        ReflectionAccess.TrySet(rectTransform, anchorMax, "anchorMax");
        ReflectionAccess.TrySet(rectTransform, pivot, "pivot");
        ReflectionAccess.TrySet(rectTransform, position, "anchoredPosition");
        ReflectionAccess.TrySet(rectTransform, size, "sizeDelta");
    }

    private object CreateVector2(double x, double y) =>
        ReflectionAccess.CreateVector(_vector2Type, x, y, 0.0);

    private object CreateColor(float red, float green, float blue, float alpha) =>
        Activator.CreateInstance(_colorType, red, green, blue, alpha)
        ?? throw new InvalidOperationException("Could not construct UnityEngine.Color.");

    private object GetMarkerColor(string state) => state switch
    {
        DiagnosticMarkerStates.Ready => CreateColor(0.27f, 0.84f, 0.44f, 1.0f),
        DiagnosticMarkerStates.CursorMiss => CreateColor(0.92f, 0.30f, 0.29f, 1.0f),
        DiagnosticMarkerStates.SelectorConflict => CreateColor(1.0f, 0.84f, 0.31f, 1.0f),
        DiagnosticMarkerStates.Direction => CreateColor(1.0f, 0.57f, 0.30f, 1.0f),
        DiagnosticMarkerStates.RetainedRange => CreateColor(0.72f, 0.50f, 1.0f, 1.0f),
        DiagnosticMarkerStates.OutOfRange => CreateColor(0.59f, 0.63f, 0.67f, 1.0f),
        DiagnosticMarkerStates.Cooldown or
        DiagnosticMarkerStates.Busy or
        DiagnosticMarkerStates.Blocked => CreateColor(0.87f, 0.41f, 0.86f, 1.0f),
        _ => CreateColor(0.20f, 0.79f, 0.92f, 1.0f),
    };

    private static void SetEnabled(ScreenGraphic graphic, bool enabled)
    {
        if (!ReflectionAccess.TrySet(graphic.Component, enabled, "enabled"))
        {
            ReflectionAccess.Invoke(graphic.GameObject, "SetActive", enabled);
        }
    }

    private static MethodInfo? FindGenericAddComponent(Type gameObjectType, Type componentType)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return gameObjectType.GetMethods(flags)
            .FirstOrDefault(method =>
                method.Name == "AddComponent" &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 &&
                method.GetParameters().Length == 0)
            ?.MakeGenericMethod(componentType);
    }

    private static MethodInfo? FindGenericBuiltinResource(Type resourcesType, Type resourceType)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        return resourcesType.GetMethods(flags)
            .FirstOrDefault(method =>
                method.Name == "GetBuiltinResource" &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 &&
                method.GetParameters().Length == 1)
            ?.MakeGenericMethod(resourceType);
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: not null }
            ? exception.InnerException
            : exception;

    private sealed class ScreenGraphic
    {
        public ScreenGraphic(object gameObject, object rectTransform, object component)
        {
            GameObject = gameObject;
            RectTransform = rectTransform;
            Component = component;
        }

        public object GameObject { get; }

        public object RectTransform { get; }

        public object Component { get; }
    }
}
