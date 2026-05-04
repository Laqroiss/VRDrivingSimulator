# VR Sim Dashboard — Unity integration

Single self-contained HTML file — zero external dependencies, no JSX, no React, no CDN.
Drop it into Unity and drive it from C# via any WebView package.

## Quick start

1. Copy `Dashboard.html` into `Assets/StreamingAssets/Dashboard/Dashboard.html`.
2. Pick a WebView package (pick ONE):
   - **Vuplex 3D WebView** — paid, most robust for VR, renders to a texture.
   - **UniWebView** — paid, iOS/Android focus.
   - **gree/unity-webview** — free, works but less polished.
3. Place a quad in your cockpit where the tablet sits (size ~0.25 × 0.12 m for a 7" tablet).
4. Point the WebView at the streaming-asset URL for `Dashboard.html`.

## Driving the dashboard from Unity

The dashboard exposes a global JS API:

```js
window.Dashboard.set({ speed: 85, rpm: 4200, gear: 'D', signalL: false, signalR: true });
```

All fields are optional — only the keys you pass are updated. `gear` is one of `'P' | 'R' | 'N' | 'D'`.

### Vuplex example

```csharp
using Vuplex.WebView;

public class DashboardBridge : MonoBehaviour {
    public CanvasWebViewPrefab webview;   // or a BaseWebView on a quad
    public CarController car;             // your own car script

    async void Start() {
        await webview.WaitUntilInitialized();
    }

    void LateUpdate() {
        var js = $@"window.Dashboard.set({{
            speed: {car.SpeedKmh:F0},
            rpm:   {car.Rpm:F0},
            gear:  '{car.Gear}',
            signalL: {car.SignalLeft.ToString().ToLower()},
            signalR: {car.SignalRight.ToString().ToLower()}
        }});";
        webview.WebView.ExecuteJavaScript(js);
    }
}
```

### gree/unity-webview example

```csharp
webView.EvaluateJS($"window.Dashboard.set({{speed:{speed},rpm:{rpm},gear:'{gear}'}});");
```

## Demo mode

`Dashboard.html` ships with a self-contained keyboard sim so you can open it in any browser and verify it works:

- `W` / `↑` — gas
- `S` / `↓` — brake
- `A` / `D` — left / right turn signal
- `P` / `R` / `N` — park / reverse / neutral
- `Shift+D` — drive

**Turn demo mode off for Unity**: open `Dashboard.html` and change `const DEMO = true;` to `const DEMO = false;`. That's it — no other cleanup needed.

## Notes

- Layout is fully responsive (uses `vh` units) — it scales to any quad size or aspect ratio close to 2:1.
- Redline is 6500 RPM. Arc fill + RPM number both turn red past that.
- Green turn signals blink on a 420ms cycle independently of Unity's frame rate.
- All colors are defined inline in the `<style>` block at the top of `Dashboard.html` if you want to rebrand.
