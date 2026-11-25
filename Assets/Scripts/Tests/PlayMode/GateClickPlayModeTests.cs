#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

// PlayMode – bấm Cổng thành trên bản đồ mở màn kiểm duyệt.
public class PM_GateClickTests : GameSessionFixture
{
    private Mouse _mouse;
    private InputSettings.BackgroundBehavior _background;
    private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

    [SetUp]
    public void AddMouse()
    {
        _background = InputSystem.settings.backgroundBehavior;
        _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _mouse = InputSystem.AddDevice<Mouse>("TestMouse");
        _mouse.MakeCurrent();
    }

    [TearDown]
    public void RemoveMouse()
    {
        InputSystem.RemoveDevice(_mouse);
        InputSystem.settings.backgroundBehavior = _background;
        InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
    }

    private IEnumerator MoveTo(Vector3 world, bool pressed)
    {
        Vector2 screen = Camera.main.WorldToScreenPoint(world);
        var state = new MouseState { position = screen };
        InputSystem.QueueStateEvent(_mouse, pressed ? state.WithButton(MouseButton.Left) : state);
        yield return null;
        _mouse.MakeCurrent();
        yield return null;
    }

    private static IEnumerator FocusGate()
    {
        Transform gate = Object.FindFirstObjectByType<ImmigrationGate>().transform;
        Camera.main.orthographicSize = 3.5f;
        Camera.main.transform.position = new Vector3(gate.position.x, gate.position.y + 0.5f, -10f);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator GC01_ReChuotVaBamCong_MoKiemDuyet()
    {
        yield return FocusGate();
        var ui = Object.FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
        Assert.IsFalse(ui.IsOpen, "Màn Cổng không được mở sẵn khi vào scene");

        SpriteRenderer door = Object.FindFirstObjectByType<ImmigrationGate>().GetComponentInChildren<SpriteRenderer>();
        Vector3 point = door.bounds.center;
        yield return MoveTo(point, false);
        Assert.IsTrue(ImmigrationGate.IsPointerOver, "Rê chuột vào cổng phải nhận ra");
        Assert.AreNotEqual(Color.white, door.color, "Cổng phải sáng lên khi rê chuột");

        yield return MoveTo(point, true);
        yield return MoveTo(point, false);
        Assert.IsTrue(ui.IsOpen, "Bấm cổng phải mở màn kiểm duyệt");
        Assert.AreEqual(Color.white, door.color, "Mở màn kiểm duyệt rồi thì thôi sáng");
    }

    [UnityTest]
    public IEnumerator GC02_DangDatCongTrinh_HoacBamNgoai_KhongMo()
    {
        yield return FocusGate();
        var ui = Object.FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
        Transform gate = Object.FindFirstObjectByType<ImmigrationGate>().transform;

        Vector3 outside = gate.position + new Vector3(-1.5f, 1.5f, 0f);
        yield return MoveTo(outside, true);
        yield return MoveTo(outside, false);
        Assert.IsFalse(ui.IsOpen, "Bấm ngoài cổng không được mở");

        Placement.BeginPlacement(BuildingType.House);
        Vector3 point = gate.GetComponentInChildren<SpriteRenderer>().bounds.center;
        yield return MoveTo(point, true);
        yield return MoveTo(point, false);
        Assert.IsFalse(ui.IsOpen, "Đang đặt công trình thì bấm cổng không được mở");
    }
}
#endif
