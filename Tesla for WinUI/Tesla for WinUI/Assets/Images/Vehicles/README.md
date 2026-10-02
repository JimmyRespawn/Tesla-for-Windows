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

Model identity correction: Y uses the 2025 Juniper light-bar front and taller crossover proportions; S uses a long low fastback, nose slit and silver multi-spoke wheels; X uses the large panoramic SUV cabin and falcon-wing rear doors. Built-in image generation used separate model-specific prompts without the Model 3 reference. Prompt common constraints: white satin paint, elevated front-left camera, locked 2x2 door-state atlas, flat green key background. Y/X backgrounds were regenerated before approved local keying; dark tinted-window pixels are preserved and despilled. These are representative illustrations, not VIN/model-year matched assets. Native soft shadow retained; charger anchors adjusted per model.

Cybertruck added: built-in image generation, model-specific angular satin stainless-steel pickup, closed black tonneau cover, black off-road tires, elevated front-left camera and fixed 2x2 left-door states. Prompt requested flat green background with no shadow/floor/text; background-only correction preceded approved local chroma-key extraction. Native shadow and charging overlay retained, port anchor tailored to the left-rear arch. cybertruck-closed.png is the first atlas cell for legacy Image bindings. Illustration only, not exact trim or body configuration.

Climate bird-view set:
Five 1024x1536 RGBA assets (*-bird.png) generated with the built-in image tool.
Prompt set: separate Model 3/Y/S/X/Cybertruck, strict orthographic top-down,
front up, closed doors, transparent roof cutaway showing neutral dark cabin,
satin white body (bare stainless steel for Cybertruck), pure green key background,
no labels/floor/shadow. Previously approved local keying/despill applied.
Model X shows a representative six-seat layout; no VIN-specific seat configuration.
ClimateContentDialog selects via VehicleModel passed from CarData.vehicle_config.
Unknown models hide the illustration rather than showing the wrong vehicle.
The old TelsaBirdViewPhoto.webp is retained but no longer used by this dialog.
Preview: bird-preview.jpg. C# syntax, XAML/project XML, asset presence and alpha
checks passed; UWP device validation remains necessary.
