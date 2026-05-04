# VR Driving Simulator вЂ”  

## 
VR-     ( ).
  ,   (, / ,  , ,  )   .

## 
- **Unity 6** (6000.0.43f1)
- **C#** (MonoBehaviour)
- **URP** (Universal Render Pipeline 17.0.4)
- **XR Interaction Toolkit** 3.0.10 + Mock HMD вЂ” VR-
- **Input System** 1.13.1 вЂ”     legacy `Input.GetKey`
- **RoadArchitect** вЂ”   ( )
- **TextMeshPro** вЂ” UI 

## 

```
ввввввввввввввввввввввввввввввввввввввввввввввввввв
в                   EXAM LAYER                    в
в  ExamManager (Singleton) в†’     в
в  ExamTrigger   в†’ /            в
в  ExamUI        в†’ HUD, ,        в
в  StatusPanel   в†’               в
ввввввввввввввввввввввввввввввввввввввввввввввввввв
в                EXERCISE LAYER                   в
в  ParkingZone         в†’  (/.) в
в  RailwayCrossing     в†’ /       в
в  EmergencyStop       в†’       в
в  HillStartExercise   в†’                  в
в  PedestrianExercise  в†’         в
ввввввввввввввввввввввввввввввввввввввввввввввввввв
в                TRAFFIC LAYER                    в
в  TrafficLight        в†’              в
в  TrafficIntersection в†’       в
в  RedLightDetector    в†’     в
ввввввввввввввввввввввввввввввввввввввввввввввввввв
в               VEHICLE LAYER                     в
в  Car (+ Engine, WheelProperties)                в
в    в†’    (raycast)            в
в    в†’ , ,                в
в  CarHUD          в†’ / (legacy)  в
в  CarIndicators   в†’ ,         в
в  CameraSwitch    в†’ 1st/3rd person               в
в  MouseHeadLook   в†’                    в
ввввввввввввввввввввввввввввввввввввввввввввввввввв
в               BORDURE LAYER                     в
в  BordureContact       в†’     в
в  BordureManager       в†’ - в
в  WheelBordureDetector в†’           в
в  CarBordureDetector   в†’ capsule-overlap  в
в  BordurePlacer        в†’ Editor-  в
ввввввввввввввввввввввввввввввввввввввввввввввввввв
```

##  
- **SampleScene.unity** вЂ”  ;    
- **ExamManager.Instance** вЂ” Singleton,    
- **Car.cs** вЂ”    (`Start` в†’ , `FixedUpdate` в†’ )

##  

```
[ ] в†’ Car ( ) в†’ CarHUD/ExamUI ()
                                    в†“
[Trigger-] в†’ Exercise  в†’ ExamManager.AddError / CompleteXxx
                                    в†“
ExamManager.OnError/OnSuccess в†’ ExamUI.ShowNotification
ExamManager.FinishExam        в†’ ExamUI.OnExamFinish ( )
```

##    

|  |   |
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
| Car | Engine, WheelProperties ( ) |

## 

1. **Singleton** вЂ” `ExamManager.Instance`
2. **Event-driven** вЂ” `UnityEvent`  ExamManager (`OnError`, `OnSuccess`, `OnExamStart`, `OnExamFinish`)
3. **Trigger-** вЂ” `OnTriggerEnter/Exit`      
4. **Coroutine FSM** вЂ” `IEnumerator`      (TrafficIntersection, EmergencyStop, RailwayCrossing)
5. **Raycast-** вЂ”    WheelCollider
6. **Editor-** вЂ” `BordurePlacer` + `BordurePlacerEditor` (Inspector GUI + Scene GUI)
7. **Cooldown** вЂ”     (`_lastErrorTime + _errorCooldown`)

##  ()
|  |  |
|---------|----------|
| W/S   |  /  |
| A/D |  |
| Space |   |
| Z |   |
| C |   |
| X |  |
| V |   |
|  |   |

##  
-  `Input.GetKey` (legacy)   Input System
-  `InputSystem_Actions.inputactions` ,     
-      (),    ()
- `CarHUD`  `ExamUI`   /RPM вЂ” `CarHUD`   legacy
- `BordureContact.cs`  3 : `BordureContact`, `BordureManager`, `WheelBordureDetector`
