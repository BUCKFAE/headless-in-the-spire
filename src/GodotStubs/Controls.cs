// Control branch of the type tree (Control → Range/Label/.../Container).
// See Nodes.cs for the general approach and the MethodName/PropertyName/
// SignalName pattern.

namespace Godot;

public class Control : CanvasItem
{
    public new class MethodName : CanvasItem.MethodName { }
    public new class PropertyName : CanvasItem.PropertyName { }
    public new class SignalName : CanvasItem.SignalName { }
}

public class Range : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Label : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class LineEdit : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class TextEdit : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Panel : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class ColorRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class TextureRect : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class RichTextLabel : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class Container : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class AspectRatioContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class MarginContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class FlowContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class BoxContainer : Container
{
    public new class MethodName : Container.MethodName { }
    public new class PropertyName : Container.PropertyName { }
    public new class SignalName : Container.SignalName { }
}

public class VBoxContainer : BoxContainer
{
    public new class MethodName : BoxContainer.MethodName { }
    public new class PropertyName : BoxContainer.PropertyName { }
    public new class SignalName : BoxContainer.SignalName { }
}

public class HBoxContainer : BoxContainer
{
    public new class MethodName : BoxContainer.MethodName { }
    public new class PropertyName : BoxContainer.PropertyName { }
    public new class SignalName : BoxContainer.SignalName { }
}
