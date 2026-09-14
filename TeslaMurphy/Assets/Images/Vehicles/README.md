# Vehicle appearance assets

Original illustrations generated with the built-in imagegen tool; not official
Tesla assets. Model 3, Y, S and X use a low front-left three-quarter view.
Paint, model year, wheels and trim are approximate and do not follow VIN options.

Each *-side.png is a 1536x1024 transparent RGBA atlas with 2x2 cells:
closed, left front open, left rear open, both left doors open (row major).
Model X has a raised rear falcon-wing door. Far-side doors are reported in text;
this camera cannot reliably show them. Door geometry is illustrative.

Prompt set: one atlas per model, pearl white paint, black panoramic glass,
realistic dark alloy wheels, fixed low front-left camera/body/scale/lighting,
nose to lower left, rear to upper right, four equal cells with no borders/text,
the four left-door states above; pure RGB(0,255,0) background without shadows.
Model 3 low sedan; Model Y taller crossover; Model S long fastback; Model X SUV.
User-approved local chroma key removes the green background and edge spill.
Alpha was checked and assets visually inspected; no fake checkerboard backdrop.

VehicleAppearance is a native UWP control. It crops the atlas and crossfades
known door-state changes over 420ms. It is not a rigged 3D animation.
The connected charger/cable is drawn with native XAML shapes at the left rear.
Charging alone pulses the connector; system animation preferences are honored.
Unknown door data is dimmed; unavailable models use text.
Door df/dr and pf/pr are mapped via vehicle_config.rhd.

preview.html is an offline UI simulation, not a WebView used by the application.
Old *-doors.png atlases are retained but are not packaged or used by this control.
C# syntax/XML and asset checks do not replace a Visual Studio UWP build or device test.

September 2026 style revision:
The current atlases were regenerated with the built-in image tool using the user's
reference for an elevated front-left camera, understated satin white 3D rendering,
dark aero wheels and soft body highlights. The accepted prompt requested a flat
green backdrop for the previously authorized local chroma-key extraction; generated
floor/shadow variants were rejected. Four left-door states remain in the same order.
The native control adds a separate soft translucent contact shadow beneath the car,
using layered ellipses, so no background color is baked into the PNG.
Visible model/door labels are removed; screen-reader descriptions remain.
These remain illustrative assets, with approximate model/trim differentiation.
