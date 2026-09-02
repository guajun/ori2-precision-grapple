using OriPrecisionGrapple.Core;

var tests = new (string Name, Action Run)[]
{
    ("precise target routes to grapple", PreciseTargetRoutesToGrapple),
    ("missing target routes to bash", MissingTargetRoutesToBash),
    ("route is stable while held", RouteIsStableWhileHeld),
    ("press state is observable", PressStateIsObservable),
    ("release permits a new decision", ReleasePermitsNewDecision),
    ("unavailable actions route to none", UnavailableActionsRouteToNone),
    ("hit test includes radius boundary", HitTestIncludesRadiusBoundary),
    ("hit radius scales with viewport height", HitRadiusScalesWithViewportHeight),
    ("behind-camera target is rejected", BehindCameraTargetIsRejected),
    ("invalid viewport is rejected", InvalidViewportIsRejected),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void PreciseTargetRoutesToGrapple()
{
    var state = new InputRoutingState();
    Equal(InputRoute.Grapple, state.Update(true, true, true, true));
}

static void MissingTargetRoutesToBash()
{
    var state = new InputRoutingState();
    Equal(InputRoute.Bash, state.Update(true, false, true, true));
}

static void RouteIsStableWhileHeld()
{
    var state = new InputRoutingState();
    Equal(InputRoute.Bash, state.Update(true, false, true, true));
    Equal(InputRoute.Bash, state.Update(true, true, true, true));
}

static void PressStateIsObservable()
{
    var state = new InputRoutingState();
    False(state.IsPressActive);
    state.Update(true, false, true, true);
    True(state.IsPressActive);
    state.Reset();
    False(state.IsPressActive);
}

static void ReleasePermitsNewDecision()
{
    var state = new InputRoutingState();
    Equal(InputRoute.Bash, state.Update(true, false, true, true));
    Equal(InputRoute.None, state.Update(false, true, true, true));
    Equal(InputRoute.Grapple, state.Update(true, true, true, true));
}

static void UnavailableActionsRouteToNone()
{
    var state = new InputRoutingState();
    Equal(InputRoute.None, state.Update(true, true, false, false));
}

static void HitTestIncludesRadiusBoundary()
{
    var viewport = new Viewport(1920, 1080);
    True(PrecisionHitTest.IsHit(new ScreenPoint(100, 100), new ScreenPoint(124, 100), viewport));
    False(PrecisionHitTest.IsHit(new ScreenPoint(100, 100), new ScreenPoint(124.01, 100), viewport));
}

static void HitRadiusScalesWithViewportHeight()
{
    var viewport = new Viewport(3840, 2160);
    True(PrecisionHitTest.IsHit(new ScreenPoint(100, 100), new ScreenPoint(148, 100), viewport));
    False(PrecisionHitTest.IsHit(new ScreenPoint(100, 100), new ScreenPoint(148.01, 100), viewport));
}

static void BehindCameraTargetIsRejected()
{
    False(PrecisionHitTest.IsHit(
        new ScreenPoint(100, 100),
        new ScreenPoint(100, 100, -1),
        new Viewport(1920, 1080)));
}

static void InvalidViewportIsRejected()
{
    False(PrecisionHitTest.IsHit(
        new ScreenPoint(100, 100),
        new ScreenPoint(100, 100),
        new Viewport(0, 1080)));
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true, got false.");
    }
}

static void False(bool value) => True(!value);
