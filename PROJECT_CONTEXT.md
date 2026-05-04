# VR Driving Simulator — Контекст проекта

## Назначение
VR-симулятор вождения для сдачи экзамена (аналог автодрома).
Игрок управляет автомобилем, выполняет упражнения (парковка, ж/д переезд, экстренная остановка, горка, пешеходный переход) и получает оценку.

## Стек
- **Unity 6** (6000.0.43f1)
- **C#** (MonoBehaviour)
- **URP** (Universal Render Pipeline 17.0.4)
- **XR Interaction Toolkit** 3.0.10 + Mock HMD — VR-поддержка
- **Input System** 1.13.1 — но в скриптах используется legacy `Input.GetKey`
- **RoadArchitect** — генерация дорог (внешний плагин)
- **TextMeshPro** — UI текст

## Архитектура

```
┌─────────────────────────────────────────────────┐
│                   EXAM LAYER                    │
│  ExamManager (Singleton) → состояние экзамена   │
│  ExamTrigger   → старт/финиш экзамена           │
│  ExamUI        → HUD, таймер, уведомления       │
│  StatusPanel   → чеклист упражнений             │
├─────────────────────────────────────────────────┤
│                EXERCISE LAYER                   │
│  ParkingZone         → парковка (задняя/парал.) │
│  RailwayCrossing     → ж/д переезд с поездом    │
│  EmergencyStop       → экстренная остановка     │
│  HillStartExercise   → эстакада                 │
│  PedestrianExercise  → пешеходный переход       │
├─────────────────────────────────────────────────┤
│                TRAFFIC LAYER                    │
│  TrafficLight        → один светофор            │
│  TrafficIntersection → управляет парой групп    │
│  RedLightDetector    → штраф за проезд на красный│
├─────────────────────────────────────────────────┤
│               VEHICLE LAYER                     │
│  Car (+ Engine, WheelProperties)                │
│    → кастомная физика колёс (raycast)            │
│    → подвеска, трение, следы заноса              │
│  CarHUD          → спидометр/тахометр (legacy)  │
│  CarIndicators   → поворотники, аварийка        │
│  CameraSwitch    → 1st/3rd person               │
│  MouseHeadLook   → обзор мышью                  │
├─────────────────────────────────────────────────┤
│               BORDURE LAYER                     │
│  BordureContact       → штраф при столкновении  │
│  BordureManager       → авто-настройка контактов│
│  WheelBordureDetector → детекция колесом         │
│  CarBordureDetector   → capsule-overlap детекция │
│  BordurePlacer        → Editor-тулза расстановки │
└─────────────────────────────────────────────────┘
```

## Точки входа
- **SampleScene.unity** — единственная сцена; все объекты на ней
- **ExamManager.Instance** — Singleton, центральная точка управления экзаменом
- **Car.cs** — главный контроллер авто (`Start` → инициализация, `FixedUpdate` → физика)

## Поток данных

```
[Ввод игрока] → Car (физика колёс) → CarHUD/ExamUI (отображение)
                                    ↓
[Trigger-зоны] → Exercise скрипты → ExamManager.AddError / CompleteXxx
                                    ↓
ExamManager.OnError/OnSuccess → ExamUI.ShowNotification
ExamManager.FinishExam        → ExamUI.OnExamFinish (экран результатов)
```

## Ключевые зависимости между файлами

| Файл | Зависит от |
|------|-----------|
| ExamUI | ExamManager, Car, CarIndicators |
| ExamTrigger | ExamManager, CarBordureDetector, CarIndicators |
| EmergencyStop | ExamManager, CarIndicators, CarBordureDetector |
| RailwayCrossing | ExamManager, CarBordureDetector |
| ParkingZone | ExamManager, Car |
| HillStartExercise | ExamManager, Car |
| PedestrianExercise | ExamManager, Car |
| RedLightDetector | ExamManager, TrafficLight, Car |
| StatusPanel | ExamManager |
| CarHUD | Car (Engine) |
| CarIndicators | Car |
| BordureContact | ExamManager |
| CarBordureDetector | ExamManager |
| TrafficIntersection | TrafficLight |
| Car | Engine, WheelProperties (вложенные классы) |

## Паттерны

1. **Singleton** — `ExamManager.Instance`
2. **Event-driven** — `UnityEvent` в ExamManager (`OnError`, `OnSuccess`, `OnExamStart`, `OnExamFinish`)
3. **Trigger-зоны** — `OnTriggerEnter/Exit` для детекции авто в зонах упражнений
4. **Coroutine FSM** — `IEnumerator` для таймеров и последовательной логики (TrafficIntersection, EmergencyStop, RailwayCrossing)
5. **Raycast-колёса** — кастомная физика без WheelCollider
6. **Editor-тулзы** — `BordurePlacer` + `BordurePlacerEditor` (Inspector GUI + Scene GUI)
7. **Cooldown** — защита от повторных ошибок (`_lastErrorTime + _errorCooldown`)

## Управление (клавиши)
| Клавиша | Действие |
|---------|----------|
| W/S или стрелки | Газ / тормоз |
| A/D | Руль |
| Space | Ручной тормоз |
| Z | Левый поворотник |
| C | Правый поворотник |
| X | Аварийка |
| V | Переключение камеры |
| Мышь | Обзор головой |

## Известные особенности
- Используется `Input.GetKey` (legacy) вместо нового Input System
- Файл `InputSystem_Actions.inputactions` есть, но не задействован в скриптах
- Комментарии в коде на русском (кириллица), часть отображается некорректно (кодировка)
- `CarHUD` и `ExamUI` дублируют отображение скорости/RPM — `CarHUD` выглядит как legacy
- `BordureContact.cs` содержит 3 класса: `BordureContact`, `BordureManager`, `WheelBordureDetector`
