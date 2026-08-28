# Display Brightness Compatibility

Quick Controls supports two Windows brightness-control paths. The available path depends on the display and connection.

## Laptop and built-in displays

Built-in laptop and all-in-one panels normally expose brightness through Windows Management Instrumentation (WMI). Quick Controls detects active `WmiMonitorBrightness` devices and uses the brightness levels reported by Windows.

If Windows itself can change the built-in display brightness, Quick Controls should normally be able to control it too.

## External monitors and DDC/CI

Compatible external monitors are controlled through DDC/CI using the Windows monitor configuration API. DDC/CI lets software send commands such as brightness changes over the display cable.

For external monitor brightness control:

1. Open the monitor's built-in menu using its physical controls.
2. Find **DDC/CI** and make sure it is enabled.
3. Connect the monitor with HDMI or DisplayPort when possible.
4. Reopen Quick Controls or reconnect the display so it can be detected again.

The DDC/CI setting may appear under **System**, **Other settings**, or a similar monitor menu. Its location varies by manufacturer.

## Connections that may not work

Brightness control depends on the entire signal path. It may be unavailable with:

- A monitor that does not support DDC/CI.
- A TV that does not expose monitor brightness controls.
- A dock, hub, KVM switch, or adapter that does not forward DDC/CI commands.
- Some DisplayLink or virtual displays.
- A display whose DDC/CI option is disabled.

Try a direct HDMI or DisplayPort connection when possible. If Quick Controls still cannot detect brightness support, use the monitor's physical controls; all volume features remain available.

## Multiple monitors

Quick Controls lists each brightness-capable display it detects. Select the display name in the Brightness card before adjusting it. The selected display is remembered between sessions.

Two physical monitor entries can occasionally have similar names because monitor firmware supplies those names. Disconnecting and reconnecting a display refreshes the list.
