namespace OriPrecisionBash.Core;

/// <summary>
/// Locks one action for the full duration of a physical right-button press.
/// </summary>
public sealed class InputRoutingState
{
    private bool _wasHeld;

    public InputRoute CurrentRoute { get; private set; }

    public bool IsPressActive => _wasHeld;

    public InputRoute Update(
        bool rightButtonHeld,
        bool hasPreciseGrappleTarget,
        bool grappleAvailable,
        bool bashAvailable)
    {
        if (!rightButtonHeld)
        {
            Reset();
            return InputRoute.None;
        }

        if (_wasHeld)
        {
            return CurrentRoute;
        }

        _wasHeld = true;
        CurrentRoute = hasPreciseGrappleTarget && grappleAvailable
            ? InputRoute.Grapple
            : bashAvailable
                ? InputRoute.Bash
                : InputRoute.None;

        return CurrentRoute;
    }

    public void Reset()
    {
        _wasHeld = false;
        CurrentRoute = InputRoute.None;
    }
}
