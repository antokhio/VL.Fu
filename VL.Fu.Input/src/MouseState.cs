using Stride.Core.Mathematics;
using Stride.Input;

namespace Fu.Input;

public record struct MouseState
{
    public Vector2 Position { get; set; }
    public bool IsLeft { get; set; }
    public bool IsRight { get; set; }
    public bool IsMiddle { get; set; }

    // TODO: IMPLEMENT
    // public bool IsZoom { get; set; }

    public MouseState FromMouseDevice(IMouseDevice device) => this with
    {
        Position = device.Position * device.SurfaceSize * 0.01f,
        IsLeft = device.IsButtonDown(MouseButton.Left),
        IsMiddle = device.IsButtonDown(MouseButton.Middle),
        IsRight = device.IsButtonDown(MouseButton.Right),
    };
}