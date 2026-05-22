// Control branch of the type tree (Control → Range/Label/.../Container).
// See Nodes.cs for the general approach and the MethodName/PropertyName/
// SignalName pattern.

namespace Godot;

public class Control : CanvasItem
{
    public new class MethodName : CanvasItem.MethodName
    {
        public static readonly StringName GrabFocus = new("grab_focus");
    }
    public new class PropertyName : CanvasItem.PropertyName
    {
        public static readonly StringName Size = new("size");
    }
    public new class SignalName : CanvasItem.SignalName
    {
        public static readonly StringName FocusEntered = new("focus_entered");
        public static readonly StringName FocusExited = new("focus_exited");
        public static readonly StringName GuiInput = new("gui_input");
        public static readonly StringName MouseEntered = new("mouse_entered");
        public static readonly StringName MouseExited = new("mouse_exited");
        public static readonly StringName Resized = new("resized");
    }

    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    // from: NCreature.PerformIntent (and other combat-anim sites that read
    //   the source/target screen position for VFX). The headless host never
    //   renders, but the property must exist or SwitchFromPlayerToEnemySide →
    //   ExecuteEnemyTurn throws MissingMethodException before any monster
    //   intent resolves. Returning default is harmless — no consumer reads it.
    public Vector2 GlobalPosition { get; set; }

    // from: EventSweep — PUNCH_OFF event crashed on debug/start_event with
    //   "Method not found: 'Godot.Vector2 Godot.Control.get_Scale()'.".
    //   Same shape as Node2D.Scale; real Godot's Control exposes Scale for
    //   UI transform animation. (1f, 1f) is the identity default — no
    //   visual scaling.
    public Vector2 Scale { get; set; } = new Vector2(1f, 1f);
}

public class Range : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName
    {
        public static readonly StringName ValueChanged = new("value_changed");
    }
}

public class Label : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName { }
}

public class LineEdit : Control
{
    public new class MethodName : Control.MethodName
    {
        public static readonly StringName Deselect = new("deselect");
    }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName
    {
        public static readonly StringName TextChanged = new("text_changed");
        public static readonly StringName TextSubmitted = new("text_submitted");
    }
}

public class TextEdit : Control
{
    public new class MethodName : Control.MethodName { }
    public new class PropertyName : Control.PropertyName { }
    public new class SignalName : Control.SignalName
    {
        public static readonly StringName TextChanged = new("text_changed");
    }
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
