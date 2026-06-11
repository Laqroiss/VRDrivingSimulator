# VR Driving Simulator

A VR driving-examination simulator that reproduces the official Kazakhstani practical driving
test on the Astana autodrome, detects driver errors in real time, and records each attempt for
later review. Built in Unity for a Windows PC with a VR headset, steering wheel, and pedals, and
paired with a web CRM for storing and reviewing attempts.

Developed as a bachelor's group project (Astana IT University, SE-2307) by Sartayev Miras,
Zholdasov Kanagat, and Tarshilov Sergey.

---

## Architecture

```
┌────────────────────────┐        HTTP        ┌────────────────────────┐        ┌──────────┐
│   Unity client (.exe)  │  ───────────────▶  │   CRM (Next.js)        │  ────▶ │ MongoDB  │
│   VR / desktop sim     │  ◀───────────────  │   :3000                │        │ :27017   │
│                        │                    │  student + admin web   │        └──────────┘
│  exam + error scoring  │   attempts, auth,  │  panels, 2D replay     │
│  local-first JSON save │   replay, profile  └────────────────────────┘
└────────────────────────┘
```

- **Unity client** runs the entire exam, scores errors locally, and writes each attempt to a local
  JSON file first (`%USERPROFILE%/AppData/LocalLow/Astana IT University/VR Driving Simulator/pending_attempts`),
  then syncs it to the CRM. Nothing is lost if the server is offline or the app crashes.
- **CRM** (the `crm/` folder) is a Next.js app backed by MongoDB. It serves the student cabinet,
  the instructor/admin panel with error statistics, and a 2D replay of any attempt.

---

## 1. Run the CRM

Requires Docker.

```bash
cd crm
cp .env.example .env          # then edit .env (see below)
docker-compose up --build
```

This starts two containers: the web app on **http://localhost:3000** and MongoDB on **:27017**.

`.env` keys (see `crm/.env.example`):

| Key              | Purpose                                  |
|------------------|------------------------------------------|
| `MONGODB_URI`    | Mongo connection string (default points at the bundled `mongo` container) |
| `ADMIN_PASSWORD` | Password for the instructor/admin panel  |

> `.env` is gitignored — it holds secrets and is never committed.

---

## 2. Build / run the Unity client

- **Unity version:** `6000.4.5f1` (Unity 6). Open the project root in this exact version.
- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene; already in Build Settings).
- **Build:** `File ▸ Build Settings ▸ Windows ▸ Build`. The output executable is named
  **VR Driving Simulator**.

### Point the client at your server — no rebuild required

The server address lives in **one** file: `StreamingAssets/crm.json` (shipped next to the build under
`<Build>_Data/StreamingAssets/crm.json`).

```json
{ "baseUrl": "http://localhost:3000" }
```

To target a deployed server, edit this file (e.g. `"baseUrl": "http://192.168.1.50:3000"`) and
restart the client — no Unity rebuild needed. If the file is missing or malformed, the client falls
back to `http://localhost:3000`.

> The local auth callback port (`7777`) and replay port (`7779`) are loopback-only and configured on
> the `AuthManager` / `ReplayCRMSync` components in the scene; they normally don't need changing.

---

## 3. Hardware setup

- **VR headset** via OpenXR (the project ships with the OpenXR + Oculus loaders configured). Start
  your headset's OpenXR runtime before launching.
- **Steering wheel + pedals** are read through Unity's Input System; plug them in before launch.
- The simulator also runs in flat-screen/desktop mode (mouse head-look) without a headset for
  testing.

---

## 4. Using it

1. **Student** registers / signs in (in-game panel or browser), then drives the ten exam exercises
   on the Astana autodrome. Errors (kerb contact, control-line crossings, timing, red lights, etc.)
   are detected live and penalty points accumulate per the official regulation.
2. Each attempt is saved **locally first**, then synced to the CRM. An abandoned or crashed exam
   still leaves its last snapshot in the database.
3. **Instructor/admin** reviews attempts in the CRM: error statistics, pass/fail, and a **2D replay**
   of the driven route. A **3D ghost-car replay** can also be launched back inside the simulator
   from the cabinet.

---

## Repository layout

| Path                | Contents                                             |
|---------------------|------------------------------------------------------|
| `Assets/Scripts/`   | Gameplay, exam logic, vehicle physics, CRM sync      |
| `Assets/Scenes/`    | `SampleScene.unity` (the simulator scene)            |
| `Assets/StreamingAssets/crm.json` | Server address (editable post-build)   |
| `crm/`              | Next.js CRM + Docker compose + MongoDB               |
| `Documents/`        | Thesis, abstracts, and reviews                       |

---

## Notes & future work

- Currently **HTTP** between client and CRM — fine on localhost/LAN; a production deployment over a
  network should put the CRM behind **HTTPS/TLS**.
- One autodrome (Astana) is reconstructed; night/adverse-weather driving and head-tracked mirrors
  are planned future work.
