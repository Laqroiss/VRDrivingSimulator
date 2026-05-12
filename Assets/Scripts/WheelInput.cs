using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Genius Speed Wheel 5 PRO.
/// Активен только когда Car.inputMode == Wheel.
/// Кнопки: 1=Drive  2=Reverse  3=Park  4=Аварийка  5=Лево  6=Право
/// </summary>
public class WheelInput : MonoBehaviour
{
    [Header("Ссылки")]
    public Car           car;
    public CarIndicators indicators;

    [Header("Настройки")]
    [Range(0f, 0.2f)] public float deadzone         = 0.05f;
    [Range(0.1f, 2f)] public float steerSensitivity = 1f;

    private InputDevice    _device;
    private AxisControl    _steerAxis;
    private AxisControl    _gasAxis;

    // Кнопки руля
    private ButtonControl _btn1, _btn2, _btn3, _btn4, _btn5, _btn6;

    void Start()
    {
        if (car        == null) car        = FindAnyObjectByType<Car>();
        if (indicators == null) indicators = FindAnyObjectByType<CarIndicators>();
        Connect();
        InputSystem.onDeviceChange += (d, c) =>
        {
            if (c == InputDeviceChange.Added || c == InputDeviceChange.Reconnected) Connect();
        };
    }

    void Connect()
    {
        _device = InputSystem.devices.FirstOrDefault(d =>
            d.name.ToLower().Contains("speed")  ||
            d.name.ToLower().Contains("wheel")  ||
            d.name.ToLower().Contains("genius") ||
            d.name.ToLower().Contains("akino"));

        if (_device == null) { Debug.Log("[Wheel] Не найден"); return; }

        _steerAxis = _device.TryGetChildControl<AxisControl>("stick/x");
        _gasAxis   = _device.TryGetChildControl<AxisControl>("stick/y");

        _btn1 = _device.TryGetChildControl<ButtonControl>("trigger"); // Drive      (физ. 1)
        _btn2 = _device.TryGetChildControl<ButtonControl>("button2"); // Reverse    (физ. 2)
        _btn3 = _device.TryGetChildControl<ButtonControl>("button3"); // Park       (физ. 3)
        _btn4 = _device.TryGetChildControl<ButtonControl>("button4"); // Аварийка   (физ. 4)
        _btn5 = _device.TryGetChildControl<ButtonControl>("button5"); // Лево       (физ. 5)
        _btn6 = _device.TryGetChildControl<ButtonControl>("button6"); // Право      (физ. 6)

        Debug.Log($"[Wheel] Подключён: {_device.name}");
    }

    void Update()
    {
        if (_device == null || car == null) return;
        if (car.inputMode != Car.InputMode.Wheel) return;

        ReadAxes();
        ReadButtons();
    }

    void ReadAxes()
    {
        float steer = _steerAxis != null ? _steerAxis.ReadValue() : 0f;
        float axisY = _gasAxis   != null ? _gasAxis.ReadValue()   : 0f;

        steer = ApplyDeadzone(steer * steerSensitivity, deadzone);

        car.externalSteer    = steer;
        car.externalThrottle = Mathf.Clamp01(axisY);   // >0 = газ
        car.externalBrake    = Mathf.Clamp01(-axisY);  // <0 = тормоз
    }

    void ReadButtons()
    {
        float speedKmh = car.rb != null ? car.rb.linearVelocity.magnitude * 3.6f : 0f;

        // Трансмиссия
        if (Pressed(_btn1)) SwitchMode(Car.TransmissionMode.Drive,   speedKmh);
        if (Pressed(_btn2)) SwitchMode(Car.TransmissionMode.Reverse, speedKmh);
        if (Pressed(_btn3)) SwitchMode(Car.TransmissionMode.Park,    speedKmh);

        // Световые сигналы
        if (indicators != null)
        {
            if (Pressed(_btn4)) ToggleHazard();
            if (Pressed(_btn5)) ToggleLeft();
            if (Pressed(_btn6)) ToggleRight();
        }
    }

    void SwitchMode(Car.TransmissionMode mode, float speedKmh)
    {
        // Park можно включить всегда — машина физически заблокируется в Car.cs
        if (mode != Car.TransmissionMode.Park && speedKmh >= car.shiftLockSpeed)
        {
            Debug.LogWarning($"[Wheel] Нельзя переключить на {mode} на скорости {speedKmh:F1} км/ч");
            return;
        }
        car.transmissionMode = mode;
    }

    void ToggleHazard()
    {
        if (indicators.HazardLightsOn) indicators.TurnOffHazard();
        else                           indicators.TurnOnHazard();
    }

    void ToggleLeft()
    {
        if (indicators.LeftIndicatorOn) indicators.TurnOffLeft();
        else { indicators.TurnOffRight(); indicators.TurnOnLeft(); }
    }

    void ToggleRight()
    {
        if (indicators.RightIndicatorOn) indicators.TurnOffRight();
        else { indicators.TurnOffLeft(); indicators.TurnOnRight(); }
    }

    static bool Pressed(ButtonControl btn) => btn != null && btn.wasPressedThisFrame;

    float ApplyDeadzone(float v, float dz)
    {
        if (Mathf.Abs(v) < dz) return 0f;
        return Mathf.Sign(v) * (Mathf.Abs(v) - dz) / (1f - dz);
    }
}
